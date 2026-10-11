namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

// Files of a problem's data folder. Every call checks that the problem belongs to the site.
public interface IProblemFilesService
{
    // accepted: the "ac" subfolder instead of the test data.
    Task<IReadOnlyList<string>> ListAsync(int problemId, int siteId, bool accepted = false);

    // Null when the file does not exist.
    Task<string?> ReadAsync(int problemId, int siteId, string fileName);

    Task SaveAsync(int problemId, int siteId, string fileName, Stream content);

    // False when the file does not exist.
    Task<bool> DeleteAsync(int problemId, int siteId, string fileName);
}
