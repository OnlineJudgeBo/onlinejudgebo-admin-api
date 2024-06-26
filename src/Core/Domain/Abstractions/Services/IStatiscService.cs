namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IStatiscService
{
    public Task<string> GetLast365DaysSubmissionsByMonthAsync();
    public Task<string> GetSubmissionsByLanguageAsync();
}
