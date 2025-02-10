using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Domain.Abstractions.Repositories;

public interface IScheduleManagerRepository
{

    public Task<IEnumerable<Schedule>> GetAllAsync();

    public Task<Schedule?> GetByIdAsync(int id);

    public Task AddAsync(Schedule schedule);

    public Task DeleteAsync(int id);
    Task<IEnumerable<Schedule>> GetSchedulesAsync();
}

