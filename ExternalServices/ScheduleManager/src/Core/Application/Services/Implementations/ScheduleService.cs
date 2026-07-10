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

    public async Task<IEnumerable<Schedule>> GetSchedulesWithTeachersAsync()
    {
        return await _ScheduleManagerRepository.GetSchedulesWithTeachersAsync();
    }

    public async Task DeleteScheduleAsync(int id)
    {
        await _ScheduleManagerRepository.DeleteScheduleAsync(id);
    }

    public async Task<Schedule> UpdateScheduleAsync(int id, ScheduleForCreationModel scheduleForCreation)
    {
        if (scheduleForCreation.ScheduleDays.Count != 1)
        {
            throw new ArgumentException("Debe enviar exactamente un día para actualizar un horario.");
        }

        var teacher = await GetTeacherByIdAsync(scheduleForCreation.Teacher);
        if (teacher == null)
            throw new ArgumentException("El profesor no existe.");

        var subject = await GetSubjectByIdAsync(scheduleForCreation.Subject);
        if (subject == null)
            throw new ArgumentException("La materia no existe.");

        var assistant = scheduleForCreation.Assistant.HasValue ? await GetTeacherByIdAsync(scheduleForCreation.Assistant.Value) : null;
        var startTime = TimeOnly.Parse(scheduleForCreation.ScheduleTime);
        var schedule = new Schedule
        {
            Id = id,
            DayOfWeek = scheduleForCreation.ScheduleDays.Single(),
            StartTime = startTime,
            EndTime = startTime.AddHours(2),
            Subject = subject,
            Teacher = teacher,
            Assistant = assistant
        };

        return await _ScheduleManagerRepository.UpdateScheduleAsync(id, schedule);
    }

    public async Task<Teacher> CreateTeacherAsync(string teacherName)
    {
        return await _ScheduleManagerRepository.CreateTeacherAsync(teacherName);
    }

    public async Task<Teacher> UpdateTeacherAsync(int id, string teacherName)
    {
        return await _ScheduleManagerRepository.UpdateTeacherAsync(id, teacherName);
    }

    public async Task DeleteTeacherAsync(int id)
    {
        await _ScheduleManagerRepository.DeleteTeacherAsync(id);
    }

    public async Task<IEnumerable<Teacher>> GetTeachersAsync()
    {
        return await _ScheduleManagerRepository.GetTeachersAsync();
    }

    public async Task<IEnumerable<Schedule>> GetSchedulesAsync()
    {
        return await _ScheduleManagerRepository.GetSchedulesAsync();
    }
    public async Task<Subject> CreateSubjectAsync(Subject subject)
    {
        return await _ScheduleManagerRepository.CreateSubjectsAsync(subject);
    }

    public async Task<Subject> UpdateSubjectAsync(int id, Subject subject)
    {
        return await _ScheduleManagerRepository.UpdateSubjectAsync(id, subject);
    }

    public async Task DeleteSubjectAsync(int id)
    {
        await _ScheduleManagerRepository.DeleteSubjectAsync(id);
    }

    public async Task<IEnumerable<Subject>> GetSubjectAsync()
    {
        return await _ScheduleManagerRepository.GetSubjectAsync();
    }

    public async Task<List<Schedule>> CreateScheduleAsync(ScheduleForCreationModel scheduleForCreation)
    {
        var teacher = await this.GetTeacherByIdAsync(scheduleForCreation.Teacher);
        if (teacher == null)
            throw new ArgumentException("El profesor no existe.");

        var subject = await this.GetSubjectByIdAsync(scheduleForCreation.Subject);
        if (subject == null)
            throw new ArgumentException("La materia no existe.");

        var assistant = scheduleForCreation.Assistant.HasValue ? await GetTeacherByIdAsync(scheduleForCreation.Assistant.Value) : null;
        var startTime = TimeOnly.Parse(scheduleForCreation.ScheduleTime);
        var schedules = scheduleForCreation.ScheduleDays
            .Select(day => new Schedule
            {
                DayOfWeek = day,
                StartTime = startTime,
                EndTime = startTime.AddHours(2),
                Subject = subject,
                Teacher = teacher,
                Assistant = assistant
            })
            .ToList();

        var createdSchedules = new List<Schedule>();
        foreach (var schedule in schedules)
        {
            var createdSchedule = await _ScheduleManagerRepository.CreateScheduleAsync(schedule);
            createdSchedules.Add(createdSchedule);
        }
        return createdSchedules;
    }

    public async Task<Teacher> GetTeacherByIdAsync(int id)
    {
        return await _ScheduleManagerRepository.GetTeacherByIdAsync(id);
    }

    public async Task<Subject> GetSubjectByIdAsync(int id)
    {
        return await _ScheduleManagerRepository.GetSubjectByIdAsync(id);
    }

    public async Task<Assistant?> GetAssistantBySubjectIdAsync(int subjectId)
    {
        return await _ScheduleManagerRepository.GetAssistantBySubjectIdAsync(subjectId);
    }

    public async Task<Assistant> UpsertAssistantAsync(int subjectId, string name, string schedule)
    {
        return await _ScheduleManagerRepository.UpsertAssistantAsync(subjectId, name, schedule);
    }
}
