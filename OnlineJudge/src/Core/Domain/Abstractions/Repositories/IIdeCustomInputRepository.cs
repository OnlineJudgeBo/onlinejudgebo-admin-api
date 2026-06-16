using OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IIdeCustomInputRepository
{
    Task<int> CreateCustomInputRunAsync(IdeCustomInputRunCreation run);

    Task<bool> IsCustomInputAsync(int solutionId);
}
