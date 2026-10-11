using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

// Two steps: the preview reads an uploaded package without touching the database,
// and the confirmation creates the problem from what was staged.
public interface IBocaImportService
{
    Task<BocaPreview> PreviewAsync(Stream zip, string fileName);

    // Returns the id of the created problem.
    Task<int?> ConfirmAsync(string stagingId, string userId, int siteId, IReadOnlyList<int>? classificationIds);
}
