using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

public class ScheduleService : IScheduleService
{
    private readonly IScheduleRepository _scheduleRepository;

    public ScheduleService(IScheduleRepository scheduleRepository)
    {
        _scheduleRepository = scheduleRepository ?? throw new ArgumentNullException(nameof(scheduleRepository));
    }

    public Task<IEnumerable<Schedule>> GetSchedulesWithTeachersAsync()
    {
        return _scheduleRepository.GetSchedulesWithTeachersAsync();
    }

    public Task<IEnumerable<Schedule>> GetSchedulesAsync()
    {
        return _scheduleRepository.GetSchedulesAsync();
    }

    public Task DeleteScheduleAsync(int id)
    {
        return _scheduleRepository.DeleteScheduleAsync(id);
    }

    public async Task<Schedule> UpdateScheduleAsync(int id, ScheduleForCreationModel scheduleForCreation)
    {
        EnsureSingleScheduleDay(scheduleForCreation);
        var schedule = await BuildScheduleAsync(scheduleForCreation);
        schedule.Id = id;

        return await _scheduleRepository.UpdateScheduleAsync(id, schedule);
    }

    public async Task<List<Schedule>> CreateScheduleAsync(ScheduleForCreationModel scheduleForCreation)
    {
        EnsureScheduleDays(scheduleForCreation);
        var schedules = new List<Schedule>();

        foreach (var day in scheduleForCreation.ScheduleDays)
        {
            var schedule = await BuildScheduleAsync(scheduleForCreation, day);
            schedules.Add(await _scheduleRepository.CreateScheduleAsync(schedule));
        }

        return schedules;
    }

    public Task<Teacher> CreateTeacherAsync(string teacherName)
    {
        return _scheduleRepository.CreateTeacherAsync(teacherName);
    }

    public Task<Teacher> UpdateTeacherAsync(int id, string teacherName)
    {
        return _scheduleRepository.UpdateTeacherAsync(id, teacherName);
    }

    public Task DeleteTeacherAsync(int id)
    {
        return _scheduleRepository.DeleteTeacherAsync(id);
    }

    public Task<IEnumerable<Teacher>> GetTeachersAsync()
    {
        return _scheduleRepository.GetTeachersAsync();
    }

    public Task<Subject> CreateSubjectAsync(Subject subject)
    {
        return _scheduleRepository.CreateSubjectAsync(subject);
    }

    public Task<Subject> UpdateSubjectAsync(int id, Subject subject)
    {
        return _scheduleRepository.UpdateSubjectAsync(id, subject);
    }

    public Task DeleteSubjectAsync(int id)
    {
        return _scheduleRepository.DeleteSubjectAsync(id);
    }

    public Task<IEnumerable<Subject>> GetSubjectsAsync()
    {
        return _scheduleRepository.GetSubjectsAsync();
    }

    public Task<SubjectAssistant?> GetSubjectAssistantBySubjectIdAsync(int subjectId)
    {
        return _scheduleRepository.GetSubjectAssistantBySubjectIdAsync(subjectId);
    }

    public Task<SubjectAssistant> UpsertSubjectAssistantAsync(int subjectId, string name, string schedule)
    {
        return _scheduleRepository.UpsertSubjectAssistantAsync(subjectId, name, schedule);
    }

    private async Task<Schedule> BuildScheduleAsync(ScheduleForCreationModel scheduleForCreation, string? dayOverride = null)
    {
        var teacher = await _scheduleRepository.GetTeacherByIdAsync(scheduleForCreation.Teacher)
            ?? throw new ArgumentException("El profesor no existe.");

        var subject = await _scheduleRepository.GetSubjectByIdAsync(scheduleForCreation.Subject)
            ?? throw new ArgumentException("La materia no existe.");

        var startTime = TimeOnly.Parse(scheduleForCreation.ScheduleTime);

        return new Schedule
        {
            DayOfWeek = dayOverride ?? scheduleForCreation.ScheduleDays.Single(),
            StartTime = startTime,
            EndTime = startTime.AddHours(2),
            Subject = subject,
            Teacher = teacher
        };
    }

    private static void EnsureSingleScheduleDay(ScheduleForCreationModel scheduleForCreation)
    {
        EnsureScheduleDays(scheduleForCreation);
        if (scheduleForCreation.ScheduleDays.Count != 1)
        {
            throw new ArgumentException("Debe enviar exactamente un día para actualizar un horario.");
        }
    }

    private static void EnsureScheduleDays(ScheduleForCreationModel scheduleForCreation)
    {
        if (scheduleForCreation.ScheduleDays.Count == 0)
        {
            throw new ArgumentException("Debe enviar al menos un día para crear un horario.");
        }
    }
}
