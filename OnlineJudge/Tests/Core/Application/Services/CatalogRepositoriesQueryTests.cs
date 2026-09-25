using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
using static RepositoryTestSupport;

public class ScheduleRepositoryQueryTests
{
    private static SqliteContext<ScheduleManagementDbContext> Scope() =>
        CreateSqliteContext<ScheduleManagementDbContext>(options => new SqliteScheduleManagementDbContext(options));

    [Fact]
    public async Task Lookups_ReturnNullWhenMissing()
    {
        using var scope = Scope();
        var repository = new ScheduleRepository(scope.Context, CreateMapper());

        Assert.Null(await repository.GetTeacherByIdAsync(99));
        Assert.Null(await repository.GetSubjectByIdAsync(99));
        Assert.Null(await repository.GetSubjectAssistantBySubjectIdAsync(99));
    }

    [Fact]
    public async Task Queries_ReturnSchedulesTeachersAndSubjectsWithDetails()
    {
        using var scope = Scope();
        var repository = new ScheduleRepository(scope.Context, CreateMapper());
        var teacher = await repository.CreateTeacherAsync("Ada");
        var subject = await repository.CreateSubjectAsync(new Subject { Name = "Algoritmos" });
        await repository.CreateScheduleAsync(new Schedule { DayOfWeek = "Lunes", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(10, 0), Subject = subject, Teacher = teacher });

        var withTeachers = (await repository.GetSchedulesWithTeachersAsync()).Single();
        var schedules = (await repository.GetSchedulesAsync()).Single();
        var teachers = (await repository.GetTeachersAsync()).Single();
        var subjects = (await repository.GetSubjectsAsync()).Single();

        Assert.Equal("Ada", withTeachers.Teacher!.Name);
        Assert.Equal("Algoritmos", schedules.Subject.Name);
        Assert.Equal("Lunes", teachers.Schedules.Single().DayOfWeek);
        Assert.Equal(subject.Id, subjects.Id);
        Assert.Equal("Ada", (await repository.GetTeacherByIdAsync(teacher.Id))!.Name);
        Assert.Equal("Algoritmos", (await repository.GetSubjectByIdAsync(subject.Id))!.Name);
    }

    [Fact]
    public async Task UpdateTeacherAndSubject_RenameOrThrowWhenMissing()
    {
        using var scope = Scope();
        var repository = new ScheduleRepository(scope.Context, CreateMapper());
        var teacher = await repository.CreateTeacherAsync("Ada");
        var subject = await repository.CreateSubjectAsync(new Subject { Name = "Algo" });

        Assert.Equal("Grace", (await repository.UpdateTeacherAsync(teacher.Id, "Grace")).Name);
        Assert.Equal("Grafos", (await repository.UpdateSubjectAsync(subject.Id, new Subject { Name = "Grafos" })).Name);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.UpdateSubjectAsync(99, new Subject { Name = "X" }));
    }

    [Fact]
    public async Task SubjectAssistant_JoinsMultipleRowsInOrder()
    {
        using var scope = Scope();
        scope.Context.SubjectAssistants.AddRange(
            new DbSubjectAssistant { SubjectId = 3, Name = "Ana", Schedule = "Lun 8-10" },
            new DbSubjectAssistant { SubjectId = 3, Name = "Bob", Schedule = "Mar 8-10" },
            new DbSubjectAssistant { SubjectId = 4, Name = "Otro", Schedule = "Mie" });
        await scope.Context.SaveChangesAsync();
        var repository = new ScheduleRepository(scope.Context, CreateMapper());

        var assistant = await repository.GetSubjectAssistantBySubjectIdAsync(3);

        Assert.Equal("Ana, Bob", assistant!.Name);
        Assert.Equal("Lun 8-10 | Mar 8-10", assistant.Schedule);
        Assert.Equal(3, assistant.SubjectId);
    }
}

public class TopicAndLanguageRepositoryQueryTests
{
    [Fact]
    public async Task Topics_GetAllAndClassificationById()
    {
        using var seed = new JudgeSeed();
        var p = seed.Problem("A");
        seed.Tag("Grafos", p);
        var classificationId = seed.Db.Classifications.Single().ClassificationId;
        var repository = new TopicRepository(seed.Db, CreateMapper());

        var topic = Assert.Single(await repository.GetAllTopicsAsync());

        Assert.Equal("Grafos", topic.Classifications.Single().Name);
        Assert.Equal("Grafos", (await repository.GetClassificationById(classificationId)).Name);
        Assert.Null(await repository.GetClassificationById(999));
    }

    [Fact]
    public async Task ProgrammingLanguages_ListsAll()
    {
        using var seed = new JudgeSeed();
        seed.Language(1, "C++").Language(2, "Python");

        var languages = await new ProgrammingLanguageRepository(seed.Db, CreateMapper()).GetAllProgrammingLanguageAsync();

        Assert.Equal(new[] { "C++", "Python" }, languages.Select(language => language.Name).OrderBy(name => name));
    }
}
