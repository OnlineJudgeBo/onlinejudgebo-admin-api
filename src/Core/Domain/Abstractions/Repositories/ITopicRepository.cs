using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface ITopicRepository
{
    public Task<IEnumerable<Topic>> GetAllTopicsAsync();

    public Task AddTopicToProblemAsync(int problem_id, IEnumerable<Classification> topics);
}
