using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

// Keeps an uploaded BOCA package between the preview and its confirmation.
public interface IBocaPackageStaging
{
    Task<(string StagingId, BocaPackage Package)> StageAsync(Stream zip, string fileName);

    // Null when the package is gone. ArgumentException when the id is not one StageAsync gave.
    Task<BocaPackage?> OpenAsync(string stagingId);

    // Keeps a permanent copy of the original zip, named after the problem it became.
    void Archive(string stagingId, string problemId);

    void Discard(string stagingId);
}
