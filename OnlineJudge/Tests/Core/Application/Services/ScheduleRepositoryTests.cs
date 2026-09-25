using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
using static RepositoryTestSupport;

// ScheduleRepository uses a separate DbContext (ScheduleManagementDbContext)
// and several Delete methods use ExecuteDeleteAsync — a set-based SQL
// operation the EF InMemory provider does not support at all (it throws).
// These tests run against Sqlite's in-memory mode instead, which does
// translate ExecuteDelete/ExecuteUpdate to real SQL, so the whole file uses
// one provider throughout rather than mixing two.
public class ScheduleRepositoryTests
{
    private static SqliteContext<ScheduleManagementDbContext> CreateScope() =>
        CreateSqliteContext<ScheduleManagementDbContext>(o => new SqliteScheduleManagementDbContext(o));

    [Fact]
    public async Task CreateTeacherAsync_AssignsGeneratedId()
    {
        using SqliteContext<ScheduleManagementDbContext> scope = CreateScope();
        var repository = new ScheduleRepository(scope.Context, CreateMapper());

        Teacher created = await repository.CreateTeacherAsync("Ada Lovelace");

        Assert.NotEqual(0, created.Id);
        Assert.Equal("Ada Lovelace", created.Name);
    }

    [Fact]
    public async Task CreateTeacherAsync_ReturnsExistingTeacher_WhenNameAlreadyExists()
    {
        using SqliteContext<ScheduleManagementDbContext> scope = CreateScope();
        var repository = new ScheduleRepository(scope.Context, CreateMapper());

        Teacher first = await repository.CreateTeacherAsync("Ada Lovelace");
        Teacher second = await repository.CreateTeacherAsync("Ada Lovelace");

        Assert.Equal(first.Id, second.Id);
        Assert.Single(scope.Context.Teachers);
    }

    [Fact]
    public async Task UpdateTeacherAsync_ThrowsWhenNotFound()
    {
        using SqliteContext<ScheduleManagementDbContext> scope = CreateScope();
        var repository = new ScheduleRepository(scope.Context, CreateMapper());

        await Assert.ThrowsAsync<ArgumentException>(() => repository.UpdateTeacherAsync(9999, "x"));
    }

    [Fact]
    public async Task DeleteTeacherAsync_ThrowsWhenTeacherHasSchedules()
    {
        using SqliteContext<ScheduleManagementDbContext> scope = CreateScope();
        var repository = new ScheduleRepository(scope.Context, CreateMapper());

        Teacher teacher = await repository.CreateTeacherAsync("Ada Lovelace");
        Subject subject = await repository.CreateSubjectAsync(new Subject { Name = "Algorithms" });
        await repository.CreateScheduleAsync(new Schedule
        {
            DayOfWeek = "Monday",
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Subject = subject,
            Teacher = teacher
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.DeleteTeacherAsync(teacher.Id));
    }

    [Fact]
    public async Task DeleteTeacherAsync_RemovesTeacher_WhenNoSchedules()
    {
        using SqliteContext<ScheduleManagementDbContext> scope = CreateScope();
        var repository = new ScheduleRepository(scope.Context, CreateMapper());

        Teacher teacher = await repository.CreateTeacherAsync("Ada Lovelace");

        await repository.DeleteTeacherAsync(teacher.Id);

        Assert.Empty(scope.Context.Teachers);
    }

    [Fact]
    public async Task CreateSubjectAsync_ReturnsExistingSubject_WhenNameAlreadyExists()
    {
        using SqliteContext<ScheduleManagementDbContext> scope = CreateScope();
        var repository = new ScheduleRepository(scope.Context, CreateMapper());

        Subject first = await repository.CreateSubjectAsync(new Subject { Name = "Algorithms" });
        Subject second = await repository.CreateSubjectAsync(new Subject { Name = "Algorithms" });

        Assert.Equal(first.Id, second.Id);
        Assert.Single(scope.Context.Subjects);
    }

    [Fact]
    public async Task DeleteSubjectAsync_RemovesSubject()
    {
        using SqliteContext<ScheduleManagementDbContext> scope = CreateScope();
        var repository = new ScheduleRepository(scope.Context, CreateMapper());

        Subject subject = await repository.CreateSubjectAsync(new Subject { Name = "Algorithms" });

        await repository.DeleteSubjectAsync(subject.Id);

        Assert.Empty(scope.Context.Subjects);
    }

    [Fact]
    public async Task CreateScheduleAsync_PersistsWithSubjectAndTeacherDetails()
    {
        using SqliteContext<ScheduleManagementDbContext> scope = CreateScope();
        var repository = new ScheduleRepository(scope.Context, CreateMapper());

        Teacher teacher = await repository.CreateTeacherAsync("Ada Lovelace");
        Subject subject = await repository.CreateSubjectAsync(new Subject { Name = "Algorithms" });

        Schedule created = await repository.CreateScheduleAsync(new Schedule
        {
            DayOfWeek = "Monday",
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Subject = subject,
            Teacher = teacher
        });

        Assert.NotEqual(0, created.Id);
        Assert.Equal(subject.Id, created.Subject.Id);
        Assert.Equal(teacher.Id, created.Teacher!.Id);
    }

    [Fact]
    public async Task CreateScheduleAsync_AllowsNullTeacher()
    {
        using SqliteContext<ScheduleManagementDbContext> scope = CreateScope();
        var repository = new ScheduleRepository(scope.Context, CreateMapper());

        Subject subject = await repository.CreateSubjectAsync(new Subject { Name = "Algorithms" });

        Schedule created = await repository.CreateScheduleAsync(new Schedule
        {
            DayOfWeek = "Monday",
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Subject = subject,
            Teacher = null
        });

        Assert.Null(created.Teacher);
    }

    [Fact]
    public async Task UpdateScheduleAsync_UpdatesFields_AndThrowsWhenNotFound()
    {
        using SqliteContext<ScheduleManagementDbContext> scope = CreateScope();
        var repository = new ScheduleRepository(scope.Context, CreateMapper());

        Subject subject = await repository.CreateSubjectAsync(new Subject { Name = "Algorithms" });
        Schedule created = await repository.CreateScheduleAsync(new Schedule
        {
            DayOfWeek = "Monday",
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Subject = subject,
            Teacher = null
        });

        Schedule updated = await repository.UpdateScheduleAsync(created.Id, new Schedule
        {
            DayOfWeek = "Tuesday",
            StartTime = new TimeOnly(11, 0),
            EndTime = new TimeOnly(12, 0),
            Subject = subject,
            Teacher = null
        });

        Assert.Equal("Tuesday", updated.DayOfWeek);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.UpdateScheduleAsync(9999, updated));
    }

    [Fact]
    public async Task DeleteScheduleAsync_RemovesSchedule()
    {
        using SqliteContext<ScheduleManagementDbContext> scope = CreateScope();
        var repository = new ScheduleRepository(scope.Context, CreateMapper());

        Subject subject = await repository.CreateSubjectAsync(new Subject { Name = "Algorithms" });
        Schedule created = await repository.CreateScheduleAsync(new Schedule
        {
            DayOfWeek = "Monday",
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Subject = subject,
            Teacher = null
        });

        await repository.DeleteScheduleAsync(created.Id);

        Assert.Empty(scope.Context.Schedules);
    }

    [Fact]
    public async Task UpsertSubjectAssistantAsync_ReplacesExistingAssistant()
    {
        using SqliteContext<ScheduleManagementDbContext> scope = CreateScope();
        var repository = new ScheduleRepository(scope.Context, CreateMapper());

        Subject subject = await repository.CreateSubjectAsync(new Subject { Name = "Algorithms" });

        await repository.UpsertSubjectAssistantAsync(subject.Id, "Alice", "Mon 9-10");
        SubjectAssistant updated = await repository.UpsertSubjectAssistantAsync(subject.Id, "Bob", "Tue 9-10");

        Assert.Equal("Bob", updated.Name);
        Assert.Single(scope.Context.SubjectAssistants);
    }

    [Fact]
    public async Task UpsertSubjectAssistantAsync_ClearsAssistant_WhenNameAndScheduleBlank()
    {
        using SqliteContext<ScheduleManagementDbContext> scope = CreateScope();
        var repository = new ScheduleRepository(scope.Context, CreateMapper());

        Subject subject = await repository.CreateSubjectAsync(new Subject { Name = "Algorithms" });
        await repository.UpsertSubjectAssistantAsync(subject.Id, "Alice", "Mon 9-10");

        await repository.UpsertSubjectAssistantAsync(subject.Id, "", "");

        Assert.Empty(scope.Context.SubjectAssistants);
    }
}
