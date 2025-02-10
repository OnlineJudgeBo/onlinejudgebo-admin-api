using ScheduleManager.Core.Domain.Abstractions.Repositories;
using ScheduleManager.Core.Domain.Abstractions.Services;
using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Application.Services.Implementations;
public class ScheduleService : IScheduleService
{
    private readonly IScheduleManagerRepository _ScheduleManagerRepository;

    public ScheduleService(IScheduleManagerRepository ScheduleManagerRepository)
    {
        _ScheduleManagerRepository = ScheduleManagerRepository ?? throw new ArgumentNullException(nameof(ScheduleManagerRepository));
    }
    public async Task<IEnumerable<Schedule>> GetSchedulesAsync()
    {
        return await _ScheduleManagerRepository.GetSchedulesAsync();
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
