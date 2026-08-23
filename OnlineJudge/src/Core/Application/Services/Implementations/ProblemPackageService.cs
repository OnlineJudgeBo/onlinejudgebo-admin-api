using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

// Builds/reads an ICPC Problem Package (https://icpc.io/problem-package-format) zip.
// Three deliberate deviations from the pure standard, all documented in ARCHITECTURE.md:
// - metadata.json (non-standard) carries every DB field verbatim, and is the actual
//   round-trip source when re-importing into this same system - statement/<lang>/problem.md
//   and problem.html are a best-effort, portable *rendition* for viewing elsewhere, not
//   used for reimport, since HTML-with-embedded-images has no lossless ICPC-native slot.
// - Spj=='Y' problems can't be exported: onlinejudge-kernel's "spj" binary uses HUSTOJ's own
//   ABI (spj input output user_output), not the ICPC output_validator interface, and shipping
//   the raw binary with a `validation: default` lie would be worse than refusing.
// - data/sample and data/secret keep the judge's own "<n>.in"/"<n>.out" names instead of
//   ICPC's "<n>.ans" for the answer file: these files are meant to be reusable directly
//   against onlinejudge-kernel's data/{problem_id}/ layout, by explicit request - not just
//   against ICPC-compliant tooling. Import still accepts ".ans" too, for genuine third-party
//   ICPC packages.
public class ProblemPackageService : IProblemPackageService
{
    private const string StatementLanguage = "es";

    private readonly IProblemService _problemService;
    private readonly IFileSystemLocalManagerManager _fileManager;

    public ProblemPackageService(IProblemService problemService, IFileSystemLocalManagerManager fileManager)
    {
        _problemService = problemService ?? throw new ArgumentNullException(nameof(problemService));
        _fileManager = fileManager ?? throw new ArgumentNullException(nameof(fileManager));
    }

    public async Task<byte[]> ExportProblemPackageAsync(int problemId, int siteId)
    {
        var problem = await _problemService.GetProblemByIdAsync(problemId, siteId)
            ?? throw new KeyNotFoundException($"Problem {problemId} not found.");

        if (string.Equals(problem.Spj, "Y", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "No se puede exportar un problema con juez especial (Spj=Y): el checker de este juez " +
                "usa una interfaz propia (HUSTOJ), incompatible con el output_validator de ICPC.");
        }

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "problem.yaml", BuildProblemYaml(problem));
            WriteEntry(archive, "metadata.json", BuildMetadataJson(problem));

            foreach (var sample in GetSampleCasesWithLegacyFallback(problem))
            {
                WriteEntry(archive, $"data/sample/{sample.Num}.in", sample.Input ?? string.Empty);
                WriteEntry(archive, $"data/sample/{sample.Num}.out", sample.Output ?? string.Empty);
            }

            CopySecretTestData(archive, problemId);

            var combinedHtml = BuildStatementHtml(problem);
            var (htmlWithRelativeImages, images) = await ExtractImagesAsync(combinedHtml);
            WriteEntry(archive, $"statement/{StatementLanguage}/problem.html", htmlWithRelativeImages);
            WriteEntry(archive, $"statement/{StatementLanguage}/problem.md", StripHtmlToText(combinedHtml));
            foreach (var (name, bytes) in images)
            {
                WriteEntry(archive, $"statement/{StatementLanguage}/img/{name}", bytes);
            }
        }

        return stream.ToArray();
    }

    // Problems created before the problem_sample_case table existed only have the legacy flat
    // sample_input/sample_output pair on `problem` - same fallback PublicRepository.GetProblemDetailAsync
    // already applies for the public-facing statement, kept here so export doesn't silently drop
    // the sample for every pre-existing problem.
    private static IEnumerable<ProblemSample> GetSampleCasesWithLegacyFallback(Problem problem)
    {
        if (problem.SampleCases.Count > 0)
        {
            return problem.SampleCases.OrderBy(s => s.Num);
        }

        if (string.IsNullOrWhiteSpace(problem.SampleInput) && string.IsNullOrWhiteSpace(problem.SampleOutput))
        {
            return Enumerable.Empty<ProblemSample>();
        }

        return new[]
        {
            new ProblemSample { Num = 1, Input = problem.SampleInput ?? string.Empty, Output = problem.SampleOutput ?? string.Empty },
        };
    }

    private void CopySecretTestData(ZipArchive archive, int problemId)
    {
        var files = _fileManager.ListFiles(problemId.ToString()) ?? Array.Empty<string>();
        foreach (var fileName in files)
        {
            if (fileName.Equals("sample.in", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("sample.out", StringComparison.OrdinalIgnoreCase))
            {
                continue; // already covered by data/sample from ProblemSample rows.
            }

            var bytes = _fileManager.ReadFile(problemId.ToString(), fileName);

            // Keep the judge's own file name/extension (.in/.out) instead of ICPC's .ans -
            // these files are meant to be reusable directly against onlinejudge-kernel's
            // data/{problem_id}/ layout, not just against ICPC-compliant tooling.
            WriteEntry(archive, $"data/secret/{fileName}", bytes);
        }
    }

    private static string BuildProblemYaml(Problem problem)
    {
        var name = YamlEscape(problem.Title ?? string.Empty);
        var source = YamlEscape(!string.IsNullOrWhiteSpace(problem.Source) ? problem.Source : problem.OriginSource ?? string.Empty);

        return
            $"name: \"{name}\"\n" +
            $"uuid: {Guid.NewGuid()}\n" +
            $"source: \"{source}\"\n" +
            "validation: default\n" +
            "limits:\n" +
            $"  time_limit: {problem.TimeLimit ?? 1}\n" +
            $"  memory: {problem.MemoryLimit ?? 128}\n";
    }

    private static string BuildMetadataJson(Problem problem)
    {
        var metadata = new ProblemPackageMetadata
        {
            Title = problem.Title,
            Description = problem.Description,
            Input = problem.Input,
            Output = problem.Output,
            Hint = problem.Hint,
            Source = problem.Source,
            OriginSource = problem.OriginSource,
            Defunct = problem.Defunct,
            TimeLimit = problem.TimeLimit,
            MemoryLimit = problem.MemoryLimit,
            ClassificationIds = problem.Classifications?.Select(c => c.ClassificationId).ToList() ?? new List<int>(),
        };

        return JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string BuildStatementHtml(Problem problem)
    {
        var builder = new StringBuilder();
        builder.Append("<h1>").Append(System.Net.WebUtility.HtmlEncode(problem.Title)).Append("</h1>\n");
        builder.Append(problem.Description).Append('\n');
        builder.Append("<h2>Entrada</h2>\n").Append(problem.Input).Append('\n');
        builder.Append("<h2>Salida</h2>\n").Append(problem.Output).Append('\n');
        if (!string.IsNullOrWhiteSpace(problem.Hint))
        {
            builder.Append("<h2>Hint</h2>\n").Append(problem.Hint).Append('\n');
        }

        return builder.ToString();
    }

    // ponytail: regex-based img extraction, no HTML parser dependency (none is installed and
    // AGENTS.md forbids adding one without an explicit ask). Handles data: URIs and http(s)
    // URLs; anything else (e.g. a relative path we can't resolve) is left untouched in place.
    private static readonly Regex ImgSrcPattern = new("<img[^>]+src\\s*=\\s*\"(?<src>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static async Task<(string Html, List<(string Name, byte[] Bytes)> Images)> ExtractImagesAsync(string html)
    {
        var images = new List<(string, byte[])>();
        var seen = new HashSet<string>(); // avoid re-fetching/duplicating an identical src repeated in the same statement
        var index = 0;
        using var httpClient = new HttpClient();

        var matches = ImgSrcPattern.Matches(html);
        foreach (Match match in matches)
        {
            var src = match.Groups["src"].Value;
            if (!seen.Add(src))
            {
                continue;
            }

            byte[]? bytes = null;
            string extension = "bin";

            if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = src.IndexOf(',');
                if (comma > 0)
                {
                    var header = src[5..comma]; // e.g. "image/png;base64"
                    extension = MimeTypeToExtension(header.Split(';')[0]);
                    try
                    {
                        bytes = Convert.FromBase64String(src[(comma + 1)..]);
                    }
                    catch (FormatException)
                    {
                        bytes = null;
                    }
                }
            }
            else if (src.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || src.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var response = await httpClient.GetAsync(src);
                    response.EnsureSuccessStatusCode();
                    bytes = await response.Content.ReadAsByteArrayAsync();

                    // Prefer the real Content-Type over guessing from the URL path - lots of
                    // image-serving endpoints (uploads, S3, a custom /files/{id}) have no
                    // extension in the path at all, which used to fall through to ".bin".
                    extension = MimeTypeToExtension(response.Content.Headers.ContentType?.MediaType);
                    if (extension == "bin")
                    {
                        var pathExtension = Path.GetExtension(new Uri(src).AbsolutePath).TrimStart('.');
                        if (!string.IsNullOrWhiteSpace(pathExtension))
                        {
                            extension = pathExtension.ToLowerInvariant();
                        }
                    }
                }
                catch (Exception)
                {
                    bytes = null; // best-effort: leave the original src untouched, keep exporting.
                }
            }

            if (bytes is null)
            {
                continue;
            }

            index++;
            var fileName = $"{index}.{extension}";
            images.Add((fileName, bytes));
            html = html.Replace(src, $"img/{fileName}");
        }

        return (html, images);
    }

    // Normalizes a MIME type's subtype into a file extension. Falls back to the subtype itself
    // for anything not explicitly listed (e.g. "tiff" -> "tiff"), and only gives up to "bin"
    // when there's truly no usable type at all.
    private static string MimeTypeToExtension(string? mimeType)
    {
        var subtype = mimeType?.Trim().ToLowerInvariant().Split('/').LastOrDefault();
        return subtype switch
        {
            null or "" or "octet-stream" => "bin",
            "jpeg" => "jpg",
            "svg+xml" => "svg",
            "vnd.microsoft.icon" or "x-icon" => "ico",
            _ => subtype,
        };
    }

    private static readonly Regex TagPattern = new("<[^>]+>", RegexOptions.Compiled);

    private static string StripHtmlToText(string html)
    {
        var text = TagPattern.Replace(html, " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, "[ \\t]+", " ");
        text = Regex.Replace(text, "\\n\\s*\\n+", "\n\n");
        return text.Trim();
    }

    private static string YamlEscape(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", string.Empty);

    private static void WriteEntry(ZipArchive archive, string entryName, string content) =>
        WriteEntry(archive, entryName, Encoding.UTF8.GetBytes(content));

    private static void WriteEntry(ZipArchive archive, string entryName, byte[] content)
    {
        var entry = archive.CreateEntry(entryName);
        using var entryStream = entry.Open();
        entryStream.Write(content, 0, content.Length);
    }

    public async Task<Problem> ImportProblemPackageAsync(string userId, Stream zipStream, int siteId)
    {
        var stagingDir = Path.Combine(Path.GetTempPath(), "icpc-import", Guid.NewGuid().ToString("N"));
        var extractDir = Path.Combine(stagingDir, "extracted");

        try
        {
            Directory.CreateDirectory(extractDir);
            var zipPath = Path.Combine(stagingDir, "package.zip");
            using (var fileStream = File.Create(zipPath))
            {
                await zipStream.CopyToAsync(fileStream);
            }

            ZipFile.ExtractToDirectory(zipPath, extractDir);

            var problem = BuildProblemFromPackage(extractDir);
            var created = await _problemService.CreateProblemAsync(userId, problem, siteId);

            WriteSecretTestData(extractDir, created.ProblemId!.Value);

            return created;
        }
        finally
        {
            if (Directory.Exists(stagingDir))
            {
                Directory.Delete(stagingDir, recursive: true);
            }
        }
    }

    private static Problem BuildProblemFromPackage(string extractDir)
    {
        var metadataPath = Path.Combine(extractDir, "metadata.json");
        var samples = ReadSampleCases(extractDir);

        if (File.Exists(metadataPath))
        {
            // Our own export: metadata.json is the lossless source of truth.
            var metadata = JsonSerializer.Deserialize<ProblemPackageMetadata>(File.ReadAllText(metadataPath))
                ?? throw new InvalidOperationException("metadata.json is invalid.");

            return new Problem
            {
                Title = metadata.Title ?? "Imported problem",
                Description = metadata.Description ?? string.Empty,
                Input = metadata.Input ?? string.Empty,
                Output = metadata.Output ?? string.Empty,
                Hint = metadata.Hint ?? string.Empty,
                Source = metadata.Source ?? string.Empty,
                OriginSource = metadata.OriginSource ?? string.Empty,
                TimeLimit = metadata.TimeLimit ?? 1,
                MemoryLimit = metadata.MemoryLimit ?? 128,
                Defunct = metadata.Defunct ?? "N",
                Spj = "N",
                InDate = DateTime.Now,
                SampleCases = samples,
                Classifications = metadata.ClassificationIds
                    .Select(id => new Classification { ClassificationId = id })
                    .ToList(),
            };
        }

        // Genuine third-party ICPC package (no metadata.json): best-effort from problem.yaml
        // + statement/<lang>/problem.md - no Input/Output/Hint split exists in that shape.
        var yaml = ReadProblemYaml(extractDir);
        var statementRoot = Path.Combine(extractDir, "statement");
        var statementDir = Directory.Exists(statementRoot)
            ? Directory.GetDirectories(statementRoot).FirstOrDefault()
            : null;
        var description = statementDir != null && File.Exists(Path.Combine(statementDir, "problem.md"))
            ? File.ReadAllText(Path.Combine(statementDir, "problem.md"))
            : string.Empty;

        return new Problem
        {
            Title = yaml.Name ?? "Imported problem",
            Description = description,
            Input = string.Empty,
            Output = string.Empty,
            Hint = string.Empty,
            Source = yaml.Source ?? "ICPC import",
            OriginSource = "ICPC import",
            TimeLimit = yaml.TimeLimit ?? 1,
            MemoryLimit = yaml.Memory ?? 128,
            Defunct = "N",
            Spj = "N",
            InDate = DateTime.Now,
            SampleCases = samples,
            Classifications = new List<Classification>(),
        };
    }

    private static List<ProblemSample> ReadSampleCases(string extractDir)
    {
        var sampleDir = Path.Combine(extractDir, "data", "sample");
        var samples = new List<ProblemSample>();
        if (!Directory.Exists(sampleDir))
        {
            return samples;
        }

        var num = 1;
        // ponytail: numeric-first ordering, not plain string sort - "10.in" must sort after
        // "9.in", not between "1.in" and "2.in" (string sort would scramble 10+ sample cases,
        // which is exactly what our own exporter's data/sample/{n}.in naming produces).
        var orderedInputFiles = Directory.GetFiles(sampleDir, "*.in")
            .OrderBy(f => int.TryParse(Path.GetFileNameWithoutExtension(f), out var n) ? n : int.MaxValue)
            .ThenBy(f => f, StringComparer.Ordinal);
        foreach (var inputFile in orderedInputFiles)
        {
            // Our own exports write "<n>.out" (the judge's native extension); a genuine
            // third-party ICPC package uses "<n>.ans" instead - accept either.
            var outFile = Path.ChangeExtension(inputFile, ".out");
            var ansFile = Path.ChangeExtension(inputFile, ".ans");
            var answerFile = File.Exists(outFile) ? outFile : ansFile;
            samples.Add(new ProblemSample
            {
                Num = num++,
                Input = File.ReadAllText(inputFile),
                Output = File.Exists(answerFile) ? File.ReadAllText(answerFile) : string.Empty,
            });
        }

        return samples;
    }

    private void WriteSecretTestData(string extractDir, int newProblemId)
    {
        var secretDir = Path.Combine(extractDir, "data", "secret");
        if (!Directory.Exists(secretDir))
        {
            return;
        }

        _fileManager.CreateFolder(newProblemId.ToString());

        // Test data is always text (judge input/output), consistent with WriteToFile's
        // string-content signature - never binary here since Spj (the only binary,
        // "spj") is blocked at export and this also serves genuine ICPC packages, which
        // don't carry compiled binaries in data/secret either.
        foreach (var file in Directory.GetFiles(secretDir, "*", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(file);
            var targetName = fileName.EndsWith(".ans", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileNameWithoutExtension(fileName) + ".out"
                : fileName;

            _fileManager.WriteToFile(newProblemId.ToString(), targetName, File.ReadAllText(file));
        }
    }

    private static ProblemYaml ReadProblemYaml(string extractDir)
    {
        var path = Path.Combine(extractDir, "problem.yaml");
        var result = new ProblemYaml();
        if (!File.Exists(path))
        {
            return result;
        }

        // ponytail: naive line-based subset parser (name/source/limits.time_limit/limits.memory
        // only), not a real YAML parser - no YamlDotNet dependency for one file this system
        // itself always writes in a fixed shape. Upgrade if importing hand-authored ICPC
        // packages with richer problem.yaml turns out to matter.
        string? section = null;
        foreach (var rawLine in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var indented = rawLine.StartsWith(' ') || rawLine.StartsWith('\t');
            var line = rawLine.Trim();
            var colon = line.IndexOf(':');
            if (colon < 0)
            {
                continue;
            }

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim().Trim('"');

            if (!indented)
            {
                section = value.Length == 0 ? key : null;
                if (key == "name") result.Name = value;
                if (key == "source") result.Source = value;
                continue;
            }

            if (section == "limits" && key == "time_limit" && int.TryParse(value, out var timeLimit))
            {
                result.TimeLimit = timeLimit;
            }

            if (section == "limits" && key == "memory" && int.TryParse(value, out var memory))
            {
                result.Memory = memory;
            }
        }

        return result;
    }
}

internal sealed class ProblemPackageMetadata
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Input { get; set; }
    public string? Output { get; set; }
    public string? Hint { get; set; }
    public string? Source { get; set; }
    public string? OriginSource { get; set; }
    public string? Defunct { get; set; }
    public int? TimeLimit { get; set; }
    public int? MemoryLimit { get; set; }
    public List<int> ClassificationIds { get; set; } = new();
}

internal sealed class ProblemYaml
{
    public string? Name { get; set; }
    public string? Source { get; set; }
    public int? TimeLimit { get; set; }
    public int? Memory { get; set; }
}
