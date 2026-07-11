using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IScheduleService
{
    public Task<IEnumerable<Schedule>> GetSchedulesWithTeachersAsync();
    public Task DeleteScheduleAsync(int id);
    public Task<Schedule> UpdateScheduleAsync(int id, ScheduleForCreationModel schedule);
    public Task<List<Schedule>> CreateScheduleAsync(ScheduleForCreationModel schedule);

    public Task<Teacher> CreateTeacherAsync(string teacherName);
    public Task<Teacher> UpdateTeacherAsync(int id, string teacherName);
    public Task DeleteTeacherAsync(int id);
    public Task<IEnumerable<Teacher>> GetTeachersAsync();

    public Task<IEnumerable<Schedule>> GetSchedulesAsync();
    public Task<Subject> CreateSubjectAsync(Subject subject);
    public Task<Subject> UpdateSubjectAsync(int id, Subject subject);
    public Task DeleteSubjectAsync(int id);
    public Task<IEnumerable<Subject>> GetSubjectsAsync();

    public Task<SubjectAssistant?> GetSubjectAssistantBySubjectIdAsync(int subjectId);
    public Task<SubjectAssistant> UpsertSubjectAssistantAsync(int subjectId, string name, string schedule);
}
