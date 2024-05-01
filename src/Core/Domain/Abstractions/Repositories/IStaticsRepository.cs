namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IStaticsRepository
{
    public Task<string> GetLast365DaysSubmissionsByMonthAsync();
    public Task<string> GetSubmissionsByLanguageAsync();
}
