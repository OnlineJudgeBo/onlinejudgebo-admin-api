using FluentValidation;

using ScheduleManager.Core.Domain.Abstractions.Repositories;
using ScheduleManager.Core.Domain.Abstractions.Services;
using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Application.Services.Implementations;
public class SolutionService : ISolutionService
{
    private readonly ISolutionRepository _solutionRepository;
    private readonly IValidator<Problem> _userValidation;

    public SolutionService(
        ISolutionRepository solutionRepository,
        IValidator<Problem> ProblemValidation)
    {
        _solutionRepository = solutionRepository ?? throw new ArgumentNullException(nameof(solutionRepository));
        _userValidation = ProblemValidation ?? throw new ArgumentNullException(nameof(ProblemValidation));
    }

    public async Task<int> SaveSolutionRemoteAsync(string userId, int problemId, int languageId, string source, int clientSubmitId)
    {
        Solution solution = new Solution();
        solution.UserId = userId;
        solution.ProblemId = problemId;
        solution.Time = 0;
        solution.Memory = 0;
        solution.InDate = DateTime.Now;
        solution.Result = 0;
        solution.Language = languageId;
        solution.Ip = "0.0.0.0";
        solution.CodeLength = source.Length;
        solution.Num = 0;
        solution.RemoteId = clientSubmitId;
        solution.IsRemoteOj = true;
        return await _solutionRepository.SaveSolutionAsync(solution);
    }

    public async Task UpdateSolutionRemoteAsync(Solution solutionToUpdate)
    {
        Solution solution = await _solutionRepository.GetSolutionByIdAsync(solutionToUpdate.SolutionId);
        if (solution.IsRemoteOj == false)
        {
            throw new Exception("You do not own this solution.");
        }
        solution.Time = solutionToUpdate.Time;
        solution.Memory = solutionToUpdate.Memory;
        solution.JudgeTime = solutionToUpdate.JudgeTime;
        solution.Result = solutionToUpdate.Result;
        solution.RemoteId = solution.RemoteId;
        solution.InDate = solution.InDate;
        solution.Time = solution.Time;

        await _solutionRepository.UpdateSolutionRemoteAsync(solution);
    }
}
