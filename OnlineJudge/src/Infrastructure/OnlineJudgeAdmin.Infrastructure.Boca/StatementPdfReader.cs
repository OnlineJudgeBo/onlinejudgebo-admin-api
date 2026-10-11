using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Infrastructure.Boca;

// Same PDF transcription the BOCA importer uses.
public sealed class StatementPdfReader : IStatementPdfReader
{
    public async Task<TaskStatement?> ReadAsync(string pdfPath)
    {
        var folder = Path.Combine(Path.GetTempPath(), "task-statement", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "description"));
            File.Copy(pdfPath, Path.Combine(folder, "description", "statement.pdf"));
            var description = await BocaPackageReader.ReadDescriptionAsync(folder, "statement.pdf");
            return string.IsNullOrWhiteSpace(description.Html)
                ? null
                : new TaskStatement(description.Html, description.InputHtml, description.OutputHtml, description.HintHtml);
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
