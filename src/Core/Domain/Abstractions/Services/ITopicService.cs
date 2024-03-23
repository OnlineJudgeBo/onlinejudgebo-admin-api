using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface ITopicService
{
    public Task<IEnumerable<Topic>> GetAllTopicsAsync();
}

