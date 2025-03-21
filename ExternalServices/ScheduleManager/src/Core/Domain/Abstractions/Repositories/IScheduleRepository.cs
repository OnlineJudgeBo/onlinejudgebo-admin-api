using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Domain.Abstractions.Repositories;

public interface IScheduleManagerRepository
{
    public Task<Teacher> GetTeacherByIdAsync(int id);
    public Task<Subject> GetSubjectByIdAsync(int id);
    public Task<IEnumerable<Schedule>> GetSchedulesWithTeachersAsync();
    public Task DeleteScheduleAsync(int id);
    public Task<Teacher> CreateTeacherAsync(string teacherName);
    public Task<IEnumerable<Teacher>> GetTeachersAsync();
    public Task<IEnumerable<Schedule>> GetSchedulesAsync();
    public Task<Subject> CreateSubjectsAsync(Subject subject);
    public Task<IEnumerable<Subject>> GetSubjectAsync();
    public Task<Schedule> CreateScheduleAsync(Schedule schedule);
}
