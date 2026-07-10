using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Domain.Abstractions.Services;

public interface IScheduleService
{
    public Task<Teacher> GetTeacherByIdAsync(int id);
    public Task<Subject> GetSubjectByIdAsync(int id);
    public Task<IEnumerable<Schedule>> GetSchedulesWithTeachersAsync();
    public Task DeleteScheduleAsync(int id);
    public Task<Schedule> UpdateScheduleAsync(int id, ScheduleForCreationModel schedule);
    public Task<Teacher> CreateTeacherAsync(string teacherName);
    public Task<Teacher> UpdateTeacherAsync(int id, string teacherName);
    public Task DeleteTeacherAsync(int id);
    public Task<IEnumerable<Teacher>> GetTeachersAsync();
    public Task<IEnumerable<Schedule>> GetSchedulesAsync();
    public Task<Subject> CreateSubjectAsync(Subject subject);
    public Task<Subject> UpdateSubjectAsync(int id, Subject subject);
    public Task DeleteSubjectAsync(int id);
    public Task<IEnumerable<Subject>> GetSubjectAsync();
    public Task<List<Schedule>> CreateScheduleAsync(ScheduleForCreationModel schedule);
    public Task<Assistant?> GetAssistantBySubjectIdAsync(int subjectId);
    public Task<Assistant> UpsertAssistantAsync(int subjectId, string name, string schedule);
}
