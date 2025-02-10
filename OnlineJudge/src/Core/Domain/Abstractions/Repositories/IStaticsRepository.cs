namespace ScheduleManager.Core.Domain.Abstractions.Repositories;

public interface IStaticsRepository
{
    public Task<string> GetLast365DaysSubmissionsByMonthAsync(int siteId);
    public Task<string> GetSubmissionsByLanguageAsync(int siteId);
}
