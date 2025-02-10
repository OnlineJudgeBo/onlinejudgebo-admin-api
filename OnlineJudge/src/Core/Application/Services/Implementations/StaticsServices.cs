using FluentValidation;

using ScheduleManager.Core.Domain.Abstractions.Repositories;
using ScheduleManager.Core.Domain.Abstractions.Services;
using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Application.Services.Implementations;
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
        _privilegeRepository = privilegeRepository ?? throw new ArgumentException(null, nameof(privilegeRepository));
        _userValidation = ProblemValidation ?? throw new ArgumentNullException(nameof(ProblemValidation));
    }

    public async Task<string> GetLast365DaysSubmissionsByMonthAsync(int siteId)
    {
        return await _staticsRepository.GetLast365DaysSubmissionsByMonthAsync(siteId);
    }

    public async Task<string> GetSubmissionsByLanguageAsync(int siteId)
    {
        return await _staticsRepository.GetSubmissionsByLanguageAsync(siteId);
    }
}
