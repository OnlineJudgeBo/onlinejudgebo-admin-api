using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

public class ProblemFilesService(IProblemService problemService, IFileSystemLocalManagerManager files) : IProblemFilesService
{
    public async Task<IReadOnlyList<string>> ListAsync(int problemId, int siteId, bool accepted = false)
    {
        var folder = await FolderAsync(problemId, siteId);
        return files.ListFiles(accepted ? Path.Combine(folder, "ac") : folder).Order().ToList();
    }

    public async Task<string?> ReadAsync(int problemId, int siteId, string fileName)
    {
        var folder = await FolderAsync(problemId, siteId);
        if (!files.FileExists(folder, fileName))
        {
            return null;
        }

        using var reader = new StreamReader(new MemoryStream(files.ReadFile(folder, fileName)));
        return await reader.ReadToEndAsync();
    }

    public async Task SaveAsync(int problemId, int siteId, string fileName, Stream content) =>
        await files.SaveFileAsync(await FolderAsync(problemId, siteId), fileName, content);

    public async Task<bool> DeleteAsync(int problemId, int siteId, string fileName)
    {
        var folder = await FolderAsync(problemId, siteId);
        if (!files.FileExists(folder, fileName))
        {
            return false;
        }

        files.DeleteFile(folder, fileName);
        return true;
    }

    private async Task<string> FolderAsync(int problemId, int siteId)
    {
        if (await problemService.GetProblemByIdAsync(problemId, siteId) == null)
        {
            throw new KeyNotFoundException("No se encontró el problema con ID: " + problemId);
        }

        return problemId.ToString();
    }
}
