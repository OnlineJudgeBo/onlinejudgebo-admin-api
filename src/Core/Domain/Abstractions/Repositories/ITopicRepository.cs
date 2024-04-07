using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface ITopicRepository
{
    public Task<IEnumerable<Topic>> GetAllTopicsAsync();

    public Task AddClassificationsToProblemAsync(int problem_id, IEnumerable<Classification> topics);

    public Task RemoveAllClassificationsFromProblemAsync(int problemId);
}
