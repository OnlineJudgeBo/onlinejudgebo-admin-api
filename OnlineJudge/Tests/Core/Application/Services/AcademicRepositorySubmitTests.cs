using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

public class AcademicRepositorySubmitTests
{
    private static readonly DateTime Now = DateTime.Now;

    private static AcademicRepository Repository(JudgeSeed seed) => new(seed.Db, seed.Academic);

    private static async Task<long> SeedAssignmentAsync(JudgeSeed seed, int problemId, DateTime? opensAt = null, DateTime? dueAt = null, DateTime? lateDueAt = null, bool active = true, bool visible = true, string member = "ana")
    {
        seed.Academic.Courses.Add(new DbCourse { CourseId = 7, CourseKey = "c7", InviteCode = "INV7", Name = "Curso" });
        var assignment = new DbCourseAssignment
        {
            CourseId = 7, Title = "Tarea", OpensAt = opensAt ?? Now.AddDays(-1), DueAt = dueAt ?? Now.AddDays(1), LateDueAt = lateDueAt, IsActive = active, CreatedAt = Now, CreatedByUserId = "teacher"
        };
        seed.Academic.CourseAssignments.Add(assignment);
        await seed.Academic.SaveChangesAsync();
        seed.Academic.CourseAssignmentProblems.Add(new DbCourseAssignmentProblem { AssignmentId = assignment.AssignmentId, ProblemId = problemId, IsVisible = visible, Points = 100 });
        if (member != null)
        {
            seed.Academic.CourseUsers.Add(new DbCourseUser { CourseId = 7, UserId = member, Role = CourseRoleNames.Student });
        }

        await seed.Academic.SaveChangesAsync();
        return assignment.AssignmentId;
    }

    private static AcademicSubmissionRequest Request(int problemId, long? assignmentId = null, long? courseId = null, int? contestId = null) => new()
    {
        ProblemId = problemId, SourceCode = "int main(){}", LanguageId = 1, AssignmentId = assignmentId, CourseId = courseId, ContestId = contestId, ClientIp = "1.1.1.1"
    };

    [Fact]
    public async Task Submit_PracticeStoresSolutionAndSourceWithoutCourseContext()
    {
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("A");

        var response = await Repository(seed).SubmitAsync(1, "ana", Request(p1));

        var solution = await seed.Db.Solutions.AsNoTracking().SingleAsync();
        Assert.Equal(response.SolutionId, solution.SolutionId);
        Assert.Equal("ana", solution.UserId);
        Assert.Null(solution.ContestId);
        Assert.Equal("int main(){}", (await seed.Db.SourceCodes.AsNoTracking().SingleAsync()).Source);
        Assert.Empty(await seed.Academic.CourseSubmissionContexts.ToListAsync());
    }

    [Fact]
    public async Task Submit_AssignmentRecordsCourseContext()
    {
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("A");
        var assignmentId = await SeedAssignmentAsync(seed, p1);

        var response = await Repository(seed).SubmitAsync(1, "ana", Request(p1, assignmentId));

        var context = await seed.Academic.CourseSubmissionContexts.SingleAsync();
        Assert.Equal(response.SolutionId, context.SolutionId);
        Assert.Equal(7, context.CourseId);
        Assert.Equal(assignmentId, context.AssignmentId);
    }

    [Fact]
    public async Task Submit_RejectsContestId()
    {
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("A");
        var contest = seed.Contest("Final", Now.AddHours(-1), Now.AddHours(1));

        await Assert.ThrowsAsync<ArgumentException>(() => Repository(seed).SubmitAsync(1, "ana", Request(p1, contestId: contest)));
        Assert.Empty(await seed.Db.Solutions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Submit_RejectsProblemNotActiveInSite()
    {
        using var seed = new JudgeSeed();
        var inactive = seed.Problem("A", active: false);
        var otherSite = seed.Problem("B", siteId: 2);

        await Assert.ThrowsAsync<ArgumentException>(() => Repository(seed).SubmitAsync(1, "ana", Request(inactive)));
        await Assert.ThrowsAsync<ArgumentException>(() => Repository(seed).SubmitAsync(1, "ana", Request(otherSite)));
    }

    [Fact]
    public async Task Submit_ValidatesAssignment()
    {
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("A");
        var p2 = seed.Problem("B");
        var assignmentId = await SeedAssignmentAsync(seed, p1);
        var repository = Repository(seed);

        Assert.Equal("Tarea no encontrada.", (await Assert.ThrowsAsync<ArgumentException>(() => repository.SubmitAsync(1, "ana", Request(p1, 999)))).Message);
        Assert.Equal("La tarea no pertenece al curso especificado.", (await Assert.ThrowsAsync<ArgumentException>(() => repository.SubmitAsync(1, "ana", Request(p1, assignmentId, courseId: 8)))).Message);
        Assert.Equal("El problema no pertenece a la tarea especificada.", (await Assert.ThrowsAsync<ArgumentException>(() => repository.SubmitAsync(1, "ana", Request(p2, assignmentId)))).Message);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.SubmitAsync(1, "outsider", Request(p1, assignmentId)));
    }

    [Fact]
    public async Task Submit_HiddenAssignmentProblemIsRejected()
    {
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("A");
        var assignmentId = await SeedAssignmentAsync(seed, p1, visible: false);

        await Assert.ThrowsAsync<ArgumentException>(() => Repository(seed).SubmitAsync(1, "ana", Request(p1, assignmentId)));
    }

    [Theory]
    [InlineData("inactive", "Esta tarea está inactiva y no acepta envíos.")]
    [InlineData("upcoming", "Esta tarea todavía no acepta envíos.")]
    [InlineData("finished", "Esta tarea ya finalizó y no acepta envíos.")]
    public async Task Submit_AssignmentWindowIsEnforced(string state, string message)
    {
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("A");
        var assignmentId = state switch
        {
            "inactive" => await SeedAssignmentAsync(seed, p1, active: false),
            "upcoming" => await SeedAssignmentAsync(seed, p1, opensAt: Now.AddDays(1), dueAt: Now.AddDays(2)),
            _ => await SeedAssignmentAsync(seed, p1, opensAt: Now.AddDays(-3), dueAt: Now.AddDays(-2), lateDueAt: Now.AddDays(-1)),
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Repository(seed).SubmitAsync(1, "ana", Request(p1, assignmentId)));

        Assert.Equal(message, error.Message);
    }

    [Fact]
    public async Task Submit_LateWindowStillAcceptsSubmissions()
    {
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("A");
        var assignmentId = await SeedAssignmentAsync(seed, p1, opensAt: Now.AddDays(-3), dueAt: Now.AddDays(-1), lateDueAt: Now.AddDays(1));

        var response = await Repository(seed).SubmitAsync(1, "ana", Request(p1, assignmentId));

        Assert.True(response.SolutionId > 0);
    }

    [Fact]
    public async Task Submit_CourseWithoutAssignmentRequiresMembershipButStoresNoContext()
    {
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("A");
        await SeedAssignmentAsync(seed, p1);
        var repository = Repository(seed);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.SubmitAsync(1, "outsider", Request(p1, courseId: 7)));
        await repository.SubmitAsync(1, "ana", Request(p1, courseId: 7));

        Assert.Empty(await seed.Academic.CourseSubmissionContexts.ToListAsync());
    }
}
