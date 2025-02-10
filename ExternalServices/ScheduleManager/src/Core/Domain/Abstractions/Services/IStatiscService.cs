namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IStatiscService
{
    public Task<string> GetLast365DaysSubmissionsByMonthAsync(int siteId);
    public Task<string> GetSubmissionsByLanguageAsync(int siteId);
}
