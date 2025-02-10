using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Domain.Abstractions.Services;

public interface IScheduleService
{
    Task<IEnumerable<Schedule>> GetSchedulesAsync();
    Task<Schedule?> GetScheduleByIdAsync(string scheduleId);
    Task<Schedule> CreateScheduleAsync(Schedule scheduleDto);
    Task<bool> DeleteScheduleAsync(string scheduleId);
}
