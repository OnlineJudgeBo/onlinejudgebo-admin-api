using FluentValidation;

using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
public class StaticsServices : IStatiscService
{
    private readonly IStaticsRepository _staticsRepository;
    private readonly IPrivilegeRepository _privilegeRepository;
    private readonly IValidator<Problem> _userValidation;

    public StaticsServices(
        IPrivilegeRepository privilegeRepository,
        IStaticsRepository staticsRepository,
        IValidator<Problem> ProblemValidation)
    {
        _staticsRepository = staticsRepository ?? throw new ArgumentNullException(nameof(staticsRepository));
        _privilegeRepository = privilegeRepository ?? throw new ArgumentException(nameof(privilegeRepository));
        _userValidation = ProblemValidation ?? throw new ArgumentNullException(nameof(ProblemValidation));
    }

    public async Task<string> GetLast365DaysSubmissionsByMonthAsync()
    {
        return await _staticsRepository.GetLast365DaysSubmissionsByMonthAsync();
    }

    public async Task<string> GetSubmissionsByLanguageAsync()
    {
        return await _staticsRepository.GetSubmissionsByLanguageAsync();
    }
}
