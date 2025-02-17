using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ScheduleManager.Core.Domain.Abstractions.Repositories;
using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Infrastructure.Database.Implementations;

public class ScheduleManagerRepository : IScheduleManagerRepository
{
    private readonly ScheduleDbContext _context;
    private readonly IMapper _mapper;

    public ScheduleManagerRepository(ScheduleDbContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<IEnumerable<Schedule>> GetSchedulesAsync()
    {
        var schedules = await _context.Schedules.ToListAsync();
        return null;
    }

    public Task AddAsync(Schedule schedule)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(int id)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<Schedule>> GetAllAsync()
    {
        //IEnumerable<DbSchedule> roles = await _context.Schedules;
        //return _mapper.Map<IEnumerable<Core.Domain.Models.Schedule>>(roles);
        throw new NotImplementedException();
    }

    public Task<Schedule?> GetByIdAsync(int id)
    {
        throw new NotImplementedException();
    }

}
