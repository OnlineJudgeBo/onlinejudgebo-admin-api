using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Domain.Abstractions.Services;

public interface IScheduleService
{
    public Task<Teacher> GetTeacherByIdAsync(int id);
    public Task<Subject> GetSubjectByIdAsync(int id);
    public Task<IEnumerable<Schedule>> GetSchedulesWithTeachersAsync();
    public Task DeleteScheduleAsync(int id);
    public Task<Teacher> CreateTeacherAsync(string teacherName);
    public Task<IEnumerable<Teacher>> GetTeachersAsync();
    public Task<IEnumerable<Schedule>> GetSchedulesAsync();
    public Task<Subject> CreateSubjectAsync(Subject subject);
    public Task<IEnumerable<Subject>> GetSubjectAsync();
    public Task<List<Schedule>> CreateScheduleAsync(ScheduleForCreationModel schedule);
}
