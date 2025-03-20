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

    public async Task<Teacher> CreateTeacherAsync(string teacherName)
    {
        return await _ScheduleManagerRepository.CreateTeacherAsync(teacherName);
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

        var schedules = scheduleForCreation.ScheduleDays
             .Select(day => new Schedule
             {
                 DayOfWeek = day,
                 StartTime = TimeOnly.Parse(scheduleForCreation.ScheduleTime),
                 EndTime = TimeOnly.Parse(scheduleForCreation.ScheduleTime).AddHours(2),
                 Subject = subject,
                 Teacher = teacher
             }).ToList();

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
}
