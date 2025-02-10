using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IScheduleRepository
{

    public Task<IEnumerable<Schedule>> GetAllAsync();

    public Task<Schedule?> GetByIdAsync(int id);

    public Task AddAsync(Schedule schedule);

    public Task DeleteAsync(int id);
}

