using System.IO.Compression;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Infrastructure.Boca;

public sealed class BocaPackageStaging : IBocaPackageStaging
{
    private static readonly string StagingRoot = Path.Combine(Path.GetTempPath(), "boca-import");
    private const string ArchiveRoot = "/boca-archive";

    public async Task<(string StagingId, BocaPackage Package)> StageAsync(Stream zip, string fileName)
    {
        var stagingId = Guid.NewGuid().ToString("N");
        var stagingDir = Path.Combine(StagingRoot, stagingId);
        try
        {
            Directory.CreateDirectory(stagingDir);
            // Kept (not deleted) until the confirmation: on success it's copied to ArchiveRoot
            // under its own name, so the original package survives even though
            // the extracted folder gets thrown away.
            var zipPath = Path.Combine(stagingDir, Path.GetFileName(fileName));
            await using (var stream = File.Create(zipPath))
            {
                await zip.CopyToAsync(stream);
            }

            var extractDir = Path.Combine(stagingDir, "extracted");
            ZipFile.ExtractToDirectory(zipPath, extractDir);
            return (stagingId, await ReadAsync(extractDir, fileName));
        }
        catch
        {
            Discard(stagingId);
            throw;
        }
    }

    public async Task<BocaPackage?> OpenAsync(string stagingId)
    {
        var extractDir = Path.Combine(StagingDir(stagingId), "extracted");
        return Directory.Exists(extractDir) ? await ReadAsync(extractDir, stagingId) : null;
    }

    // Keeps a permanent copy of the uploaded .zip (compile/run/compare scripts, every raw
    // test case, any compiled checker binary) - none of that survives into the DB or
    // /judge-data, and boca_problems/ itself isn't tracked by any git repo.
    public void Archive(string stagingId, string problemId)
    {
        // TopDirectoryOnly + GetFiles already excludes the extracted/ subdirectory; the
        // staged zip is the only file that sits directly in the staging folder.
        var originalZip = Directory.GetFiles(StagingDir(stagingId), "*", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (originalZip is null)
        {
            return;
        }

        Directory.CreateDirectory(ArchiveRoot);
        File.Copy(originalZip, Path.Combine(ArchiveRoot, $"{problemId}-{Path.GetFileName(originalZip)}"), overwrite: true);
    }

    public void Discard(string stagingId)
    {
        if (Guid.TryParseExact(stagingId, "N", out _) && Directory.Exists(Path.Combine(StagingRoot, stagingId)))
        {
            Directory.Delete(Path.Combine(StagingRoot, stagingId), recursive: true);
        }
    }

    // The id comes straight from a request and is used to build a path that later gets
    // recursively deleted - only ever accept the "N"-format GUID StageAsync hands out,
    // never a path fragment (e.g. "../../etc") from an untrusted caller.
    private static string StagingDir(string stagingId) => Guid.TryParseExact(stagingId, "N", out _)
        ? Path.Combine(StagingRoot, stagingId)
        : throw new ArgumentException("Invalid staging id.");

    private static async Task<BocaPackage> ReadAsync(string extractDir, string sourceName)
    {
        var info = BocaPackageReader.ReadProblemInfo(extractDir, Path.GetFileNameWithoutExtension(sourceName));
        var description = await BocaPackageReader.ReadDescriptionAsync(extractDir, info.DescFile);
        var sample = BocaPackageReader.ReadSamplePair(extractDir);
        var limits = BocaPackageReader.CollectLimits(extractDir);

        var reviewReasons = new List<string>();
        if (description.NeedsReview) reviewReasons.Add($"description: {description.ReviewReason}");
        if (sample.NeedsReview) reviewReasons.Add($"sample: {sample.ReviewReason}");

        return new BocaPackage(
            info.BaseName, info.FullName, description.Html, description.InputHtml, description.OutputHtml, description.HintHtml,
            sample.Input, sample.Output, limits.TimeLimitSeconds, limits.MemoryLimitMb, reviewReasons,
            BocaPackageReader.ReadAllTestCases(extractDir).Select(test => (test.Input, test.Output)).ToList());
    }
}
