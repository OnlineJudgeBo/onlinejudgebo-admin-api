using FluentValidation;

using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
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

    public async Task<int> SaveSolutionRemoteAsync(string userId, int problemId, int languageId, string source, int clientSubmitId, string clientIp = "0.0.0.0")
    {
        Solution solution = new Solution();
        solution.UserId = userId;
        solution.ProblemId = problemId;
        solution.Time = 0;
        solution.Memory = 0;
        solution.InDate = DateTime.Now;
        solution.Result = 0;
        solution.Language = languageId;
        solution.Ip = string.IsNullOrWhiteSpace(clientIp) ? "0.0.0.0" : clientIp.Trim();
        solution.CodeLength = source.Length;
        solution.Num = 0;
        solution.RemoteId = clientSubmitId;
        solution.IsRemoteOj = true;
        return await _solutionRepository.SaveSolutionAsync(solution);
    }

    public Task<Solution?> GetSolutionByIdAsync(int solutionId)
    {
        if (solutionId <= 0)
        {
            throw new ArgumentException("SolutionId inválido.");
        }

        return _solutionRepository.GetSolutionByIdAsync(solutionId);
    }

    public async Task UpdateSolutionRemoteAsync(Solution solutionToUpdate)
    {
        Solution? solution = await _solutionRepository.GetSolutionByIdAsync(solutionToUpdate.SolutionId);
        if (solution == null)
        {
            throw new ArgumentException("Solución no encontrada.");
        }

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


    public Task<AdminSubmissionAuditResponse> GetSubmissionAuditAsync(
        int siteId,
        int page,
        int pageSize,
        int? problemId,
        string? userId,
        string? clientIp)
    {
        if (siteId <= 0)
        {
            throw new ArgumentException("SiteId inválido.");
        }

        return _solutionRepository.GetSubmissionAuditAsync(siteId, page, pageSize, problemId, userId, clientIp);
    }

}
