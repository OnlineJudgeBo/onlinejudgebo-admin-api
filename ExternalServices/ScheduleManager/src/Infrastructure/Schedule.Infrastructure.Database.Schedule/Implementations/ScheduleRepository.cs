using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Infrastructure.Database.Schedule.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Schedule.Implementations;

public class ScheduleRepository : IScheduleRepository
{
    private readonly DbContext _context;
    private readonly IMapper _mapper;

    public ScheduleRepository(DbContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public Task<IEnumerable<Schedule>> GetAllAsync()
    {
        IEnumerable<DbSchedule> roles = await _context.Schedules;

        return _mapper.Map<IEnumerable<Core.Domain.Models.Schedule>>(roles);
    }

    public Task AddAsync(Schedule schedule)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(int id)
    {
        throw new NotImplementedException();
    }


    public Task<Schedule?> GetByIdAsync(int id)
    {
        throw new NotImplementedException();
    }
}

