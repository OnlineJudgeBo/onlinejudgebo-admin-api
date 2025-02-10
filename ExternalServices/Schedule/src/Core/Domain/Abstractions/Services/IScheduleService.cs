using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IScheduleService
{
    Task<IEnumerable<Schedule>> GetSchedulesAsync();
    Task<Schedule?> GetScheduleByIdAsync(string scheduleId);
    Task<Schedule> CreateScheduleAsync(Schedule scheduleDto);
    Task<bool> DeleteScheduleAsync(string scheduleId);
}
