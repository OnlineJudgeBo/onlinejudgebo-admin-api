using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

public interface IStatementPdfReader
{
    // Null when the PDF cannot be read; the statement is then left for the editor.
    Task<TaskStatement?> ReadAsync(string pdfPath);
}
