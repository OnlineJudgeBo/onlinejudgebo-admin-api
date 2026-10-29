using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IOlympiadTaskImportService
{
    // Imports an olympiad task (CMS or TPS zip) as a new problem, or over problemId replacing its statement and data.
    // InvalidDataException when it cannot be judged; KeyNotFoundException when problemId is not in this site.
    Task<Problem> ImportAsync(string userId, Stream zipStream, int siteId, int? problemId = null);
}
