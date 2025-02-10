using FluentValidation;

using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
public class ScheduleService : IScheduleService
{
    private readonly IScheduleRepository _scheduleRepository;
    private readonly IValidator<Problem> _userValidation;

    public ScheduleService(
        IScheduleRepository scheduleRepository,
        IValidator<Problem> userValidator)
    {
        _scheduleRepository = scheduleRepository ?? throw new ArgumentNullException(nameof(scheduleRepository));
        _userValidation = userValidator ?? throw new ArgumentNullException(nameof(userValidator));
    }
    public async Task<IEnumerable<Schedule>> GetSchedulesAsync()
    {
        return await _scheduleRepository.GetSchedulesAsync();
    }

    public Task<Schedule> CreateScheduleAsync(Schedule scheduleDto)
    {
        throw new NotImplementedException();
    }

    public Task<bool> DeleteScheduleAsync(string scheduleId)
    {
        throw new NotImplementedException();
    }

    public Task<Schedule?> GetScheduleByIdAsync(string scheduleId)
    {
        throw new NotImplementedException();
    }

}
