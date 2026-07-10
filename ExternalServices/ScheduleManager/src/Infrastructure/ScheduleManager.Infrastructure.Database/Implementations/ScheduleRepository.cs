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
            .Include(s => s.Assistant)
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

    public async Task<Schedule> UpdateScheduleAsync(int id, Schedule schedule)
    {
        var dbSchedule = await _context.Schedules.FirstOrDefaultAsync(s => s.Id == id);
        if (dbSchedule == null)
        {
            throw new ArgumentException("El horario no existe.");
        }

        dbSchedule.DayOfWeek = schedule.DayOfWeek;
        dbSchedule.StartTime = schedule.StartTime;
        dbSchedule.EndTime = schedule.EndTime;
        dbSchedule.SubjectId = schedule.Subject.Id;
        dbSchedule.TeacherId = schedule.Teacher?.Id;
        dbSchedule.AssistantId = schedule.Assistant?.Id;

        await _context.SaveChangesAsync();

        return _mapper.Map<Schedule>(await _context.Schedules
            .Include(s => s.Subject)
            .Include(s => s.Teacher)
            .Include(s => s.Assistant)
            .FirstAsync(s => s.Id == id));
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

    public async Task<Teacher> UpdateTeacherAsync(int id, string teacherName)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == id);
        if (teacher == null)
        {
            throw new ArgumentException("El docente no existe.");
        }

        teacher.Name = teacherName;
        await _context.SaveChangesAsync();
        return _mapper.Map<Teacher>(teacher);
    }

    public async Task DeleteTeacherAsync(int id)
    {
        var hasSchedules = await _context.Schedules.AnyAsync(schedule => schedule.TeacherId == id || schedule.AssistantId == id);
        if (hasSchedules)
        {
            throw new InvalidOperationException("No se puede eliminar un docente o auxiliar con horarios asignados.");
        }

        await _context.Teachers.Where(teacher => teacher.Id == id).ExecuteDeleteAsync();
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
        var dbSubject = await _context.Subjects.FirstOrDefaultAsync(t => t.Name == subject.Name);
        if (dbSubject == null)
        {
            dbSubject = new DbSubject { Name = subject.Name };
            await _context.Subjects.AddAsync(dbSubject);
            await _context.SaveChangesAsync();
        }
        return _mapper.Map<Subject>(dbSubject);
    }

    public async Task<Subject> UpdateSubjectAsync(int id, Subject subject)
    {
        var dbSubject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == id);
        if (dbSubject == null)
        {
            throw new ArgumentException("La materia no existe.");
        }

        dbSubject.Name = subject.Name;
        await _context.SaveChangesAsync();
        return _mapper.Map<Subject>(dbSubject);
    }

    public async Task DeleteSubjectAsync(int id)
    {
        await _context.Subjects.Where(subject => subject.Id == id).ExecuteDeleteAsync();
    }

    public async Task<Schedule> CreateScheduleAsync(Schedule schedule)
    {
        var dbSchedule = new DbSchedule
        {
            DayOfWeek = schedule.DayOfWeek,
            StartTime = schedule.StartTime,
            EndTime = schedule.EndTime,
            SubjectId = schedule.Subject.Id,
            TeacherId = schedule.Teacher?.Id,
            AssistantId = schedule.Assistant?.Id
        };

        await _context.Schedules.AddAsync(dbSchedule);
        await _context.SaveChangesAsync();

        return _mapper.Map<Schedule>(dbSchedule);
    }

    public async Task<Assistant?> GetAssistantBySubjectIdAsync(int subjectId)
    {
        var assistants = await _context.Assistants
            .Where(item => item.SubjectId == subjectId)
            .OrderBy(item => item.Id)
            .ToListAsync();

        if (assistants.Count == 0)
        {
            return null;
        }

        return new Assistant
        {
            Id = assistants.First().Id,
            SubjectId = subjectId,
            Name = string.Join(", ", assistants.Select(item => item.Name)),
            Schedule = string.Join(" | ", assistants.Select(item => item.Schedule))
        };
    }

    public async Task<Assistant> UpsertAssistantAsync(int subjectId, string name, string schedule)
    {
        await _context.Assistants.Where(item => item.SubjectId == subjectId).ExecuteDeleteAsync();

        var assistant = new DbAssistant
        {
            SubjectId = subjectId,
            Name = name,
            Schedule = schedule
        };

        if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(schedule))
        {
            await _context.Assistants.AddAsync(assistant);
        }

        await _context.SaveChangesAsync();

        return new Assistant
        {
            Id = assistant.Id,
            SubjectId = assistant.SubjectId,
            Name = assistant.Name,
            Schedule = assistant.Schedule
        };
    }
}
