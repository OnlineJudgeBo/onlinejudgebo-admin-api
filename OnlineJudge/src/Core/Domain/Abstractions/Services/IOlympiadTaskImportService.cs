using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IOlympiadTaskImportService
{
    // Imports an olympiad task (CMS or TPS zip) as a new problem. InvalidDataException when it cannot be judged.
    Task<Problem> ImportAsync(string userId, Stream zipStream, int siteId);
}
