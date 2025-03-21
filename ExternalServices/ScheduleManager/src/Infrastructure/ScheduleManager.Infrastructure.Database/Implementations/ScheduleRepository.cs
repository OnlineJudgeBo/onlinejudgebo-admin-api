using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ScheduleManager.Core.Domain.Abstractions.Repositories;
using ScheduleManager.Core.Domain.Models;
using ScheduleManager.Infrastructure.Database.Models;

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

    public async Task<Teacher> GetTeacherByIdAsync(int id)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == id);
        return _mapper.Map<Teacher>(teacher);
    }

    public async Task<Subject> GetSubjectByIdAsync(int id)
    {
        var subject = await _context.Subjects.FirstOrDefaultAsync(t => t.Id == id);
        return _mapper.Map<Subject>(subject);
    }

    public async Task<IEnumerable<Schedule>> GetSchedulesWithTeachersAsync()
    {
        var schedules = await _context.Schedules
            .Include(s => s.Subject)
            .Include(s => s.Teacher)
            .ToListAsync();

        return _mapper.Map<IEnumerable<Schedule>>(schedules);
    }

    public async Task<IEnumerable<Teacher>> GetTeachersAsync()
    {
        var teachers = await _context.Teachers
            .Include(t => t.Schedules)
            .ThenInclude(s => s.Subject)
            .ToListAsync();

        return _mapper.Map<IEnumerable<Teacher>>(teachers);
    }

    public async Task DeleteScheduleAsync(int id)
    {
        await _context.Schedules.Where(s => s.Id == id).ExecuteDeleteAsync();
    }

    public async Task<Teacher> CreateTeacherAsync(string teacherName)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Name == teacherName);
        if (teacher == null)
        {
            teacher = new DbTeacher { Name = teacherName };
            await _context.Teachers.AddAsync(teacher);
            await _context.SaveChangesAsync();
        }
        return _mapper.Map<Teacher>(teacher);
    }

    public async Task<IEnumerable<Schedule>> GetSchedulesAsync()
    {
        IEnumerable<DbSchedule> schedules = await _context.Schedules.ToListAsync();
        return _mapper.Map<IEnumerable<Schedule>>(schedules);
    }

    public async Task<IEnumerable<Subject>> GetSubjectAsync()
    {
        IEnumerable<DbSubject> subjects = await _context.Subjects.ToListAsync();
        return _mapper.Map<IEnumerable<Subject>>(subjects);
    }

    public async Task<Subject> CreateSubjectsAsync(Subject subject)
    {
        var teacher = await _context.Subjects.FirstOrDefaultAsync(t => t.Name == subject.Name);
        if (teacher == null)
        {
            teacher = new DbSubject { Name = subject.Name };
            await _context.Subjects.AddAsync(teacher);
            await _context.SaveChangesAsync();
        }
        return _mapper.Map<Subject>(teacher);
    }

    public async Task<Schedule> CreateScheduleAsync(Schedule schedule)
    {
        var dbSchedule = new DbSchedule
        {
            DayOfWeek = schedule.DayOfWeek,
            StartTime = schedule.StartTime,
            EndTime = schedule.EndTime,
            SubjectId = schedule.Subject.Id,
            TeacherId = schedule.Teacher?.Id
        };

        await _context.Schedules.AddAsync(dbSchedule);
        await _context.SaveChangesAsync();

        return _mapper.Map<Schedule>(dbSchedule);
    }
}
