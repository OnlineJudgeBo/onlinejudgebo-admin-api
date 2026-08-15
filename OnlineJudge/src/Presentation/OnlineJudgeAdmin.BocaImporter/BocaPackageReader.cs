using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

namespace OnlineJudgeAdmin.BocaImporter;

public sealed record BocaProblemInfo(string BaseName, string FullName, string? DescFile);

public sealed record BocaDescription(string Html, bool NeedsReview, string? ReviewReason);

public sealed record BocaSample(string Input, string Output, bool NeedsReview, string? ReviewReason);

public sealed record BocaTestCase(string Input, string Output);

public sealed record BocaLimits(int TimeLimitSeconds, int MemoryLimitMb);

// Reads a single BOCA problem package (see boca_problems/problemtemplate for the format).
// Scope, confirmed with the team before writing this: no test-data import (only the first
// input/output pair becomes the sample), no special judge (every import is spj="N"
// regardless of what compare/* does), and description/desc.txt|pdf is copied as-is —
// a human is expected to refine formatting/math afterwards.
public static class BocaPackageReader
{
    public static BocaProblemInfo ReadProblemInfo(string problemDir, string folderName)
    {
        var infoPath = Path.Combine(problemDir, "description", "problem.info");
        var values = new Dictionary<string, string>();

        foreach (var line in File.ReadAllLines(infoPath))
        {
            var trimmed = line.Trim();
            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0) continue;
            values[trimmed[..separatorIndex].Trim()] = trimmed[(separatorIndex + 1)..].Trim();
        }

        if (!values.TryGetValue("fullname", out var fullName))
        {
            throw new InvalidOperationException($"description/problem.info is missing fullname in {problemDir}");
        }

        // Matches DbProblem's Title column (HasMaxLength(200)) — better a clear error
        // naming the offending package than a raw MySQL "Data too long" exception.
        if (fullName.Length > 200)
        {
            throw new InvalidOperationException($"fullname is {fullName.Length} characters, longer than the 200-character title column allows, in {problemDir}");
        }

        // The template declares descfile= explicitly, but real BOCA exports we've seen
        // omit that key — fall back to whatever single non-problem.info file sits in
        // description/ (there's normally exactly one).
        values.TryGetValue("descfile", out var descFile);
        descFile ??= Directory.GetFiles(Path.Combine(problemDir, "description"))
            .Select(Path.GetFileName)
            .FirstOrDefault(name => name is not null && name != "problem.info");

        return new BocaProblemInfo(folderName, fullName, descFile);
    }

    public static BocaDescription ReadDescription(string problemDir, string? descFile)
    {
        if (descFile is null)
        {
            return new BocaDescription(string.Empty, true, "no description file found");
        }

        var descPath = Path.Combine(problemDir, "description", descFile);
        var isPdf = string.Equals(Path.GetExtension(descPath), ".pdf", StringComparison.OrdinalIgnoreCase);

        var rawText = isPdf
            ? RunAndCaptureOutput("pdftotext", ["-layout", descPath, "-"], problemDir)
            : File.ReadAllText(descPath);

        var html = ToParagraphHtml(rawText);

        if (string.IsNullOrWhiteSpace(html))
        {
            return new BocaDescription(string.Empty, true, isPdf ? "PDF produced no extractable text" : "description file is empty");
        }

        return new BocaDescription(html, isPdf, isPdf ? "sourced from PDF" : null);
    }

    // Well under MySQL's 65,535-byte TEXT column limit, generous for an actual sample.
    private const int MaxSampleChars = 20_000;

    // input/<name> paired with output/<name> — a BOCA package doesn't mark which pair is
    // "the sample", every pair is just a judge test case.
    private static IReadOnlyList<string> GetPairedTestCaseNames(string problemDir)
    {
        var inputDir = Path.Combine(problemDir, "input");
        var outputDir = Path.Combine(problemDir, "output");
        var outputNames = Directory.GetFiles(outputDir).Select(Path.GetFileName).ToHashSet();

        return Directory.GetFiles(inputDir)
            .Select(Path.GetFileName)
            .Where(name => name is not null && outputNames.Contains(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    public static BocaSample ReadSamplePair(string problemDir)
    {
        var inputDir = Path.Combine(problemDir, "input");
        var outputDir = Path.Combine(problemDir, "output");
        var pairedNames = GetPairedTestCaseNames(problemDir);

        if (pairedNames.Count == 0)
        {
            throw new InvalidOperationException($"No matching input/output pair found in {problemDir}");
        }

        // Some are multi-megabyte stress tests — the smallest pair is the best available
        // guess at something short and illustrative for the problem statement.
        var sampleName = pairedNames
            .OrderBy(name => new FileInfo(Path.Combine(inputDir, name)).Length)
            .First();

        var input = File.ReadAllText(Path.Combine(inputDir, sampleName));
        var output = File.ReadAllText(Path.Combine(outputDir, sampleName));

        if (input.Length > MaxSampleChars || output.Length > MaxSampleChars)
        {
            return new BocaSample(
                TruncateSample(input),
                TruncateSample(output),
                true,
                $"every test case is larger than {MaxSampleChars} chars — sample was truncated, replace it with a real example");
        }

        return new BocaSample(input, output, false, null);
    }

    private static string TruncateSample(string value) =>
        value.Length <= MaxSampleChars ? value : value[..MaxSampleChars] + "\n...(truncated by importer)";

    // All input/output pairs, for writing as the judge's real N.in/N.out test data —
    // unlike ReadSamplePair, nothing here is truncated or size-limited.
    public static IReadOnlyList<BocaTestCase> ReadAllTestCases(string problemDir)
    {
        var inputDir = Path.Combine(problemDir, "input");
        var outputDir = Path.Combine(problemDir, "output");

        return GetPairedTestCaseNames(problemDir)
            .Select(name => new BocaTestCase(
                File.ReadAllText(Path.Combine(inputDir, name)),
                File.ReadAllText(Path.Combine(outputDir, name))))
            .ToList();
    }

    // Runs every limits/<lang> script (trusted content — these are the problem author's own
    // files, not untrusted user code) and collapses the per-language values to a single
    // time/memory limit via max().
    public static BocaLimits CollectLimits(string problemDir)
    {
        var limitsDir = Path.Combine(problemDir, "limits");
        var timeLimit = 0;
        var memoryLimit = 128;
        var sawAny = false;

        foreach (var scriptPath in Directory.GetFiles(limitsDir))
        {
            var scriptName = Path.GetFileName(scriptPath);
            string output;
            try
            {
                // Run through bash explicitly rather than exec'ing the file directly: real
                // exported packages don't reliably keep the executable bit (or even a
                // #!/bin/bash shebang) after being zipped/unzipped, so relying on either
                // fails with EACCES or "Exec format error" depending on what's missing.
                output = RunAndCaptureOutput("bash", [scriptPath], problemDir);
            }
            catch (Exception error)
            {
                Console.WriteLine($"  ! limits/{scriptName} failed to run, skipping: {error.Message}");
                continue;
            }

            var lines = output.Trim().Split('\n');
            if (lines.Length < 3 || !int.TryParse(lines[0], out var time) || !int.TryParse(lines[2], out var memory))
            {
                Console.WriteLine($"  ! limits/{scriptName} produced unexpected output, skipping");
                continue;
            }

            sawAny = true;
            timeLimit = Math.Max(timeLimit, time);
            memoryLimit = Math.Max(memoryLimit, memory);
        }

        if (!sawAny)
        {
            Console.WriteLine($"  ! no usable limits/* scripts in {problemDir}, defaulting to timeLimit=1s memoryLimit=128MB");
            return new BocaLimits(1, 128);
        }

        return new BocaLimits(timeLimit, memoryLimit);
    }

    private static string ToParagraphHtml(string text)
    {
        var paragraphs = Regex.Split(text, @"\n\s*\n")
            .Select(paragraph => paragraph.Trim())
            .Where(paragraph => paragraph.Length > 0)
            .Select(paragraph => $"<p>{WebUtility.HtmlEncode(paragraph)}</p>");

        return string.Join('\n', paragraphs);
    }

    private static string RunAndCaptureOutput(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start {fileName}");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{fileName} exited with code {process.ExitCode}: {stderr}");
        }

        return stdout;
    }
}
