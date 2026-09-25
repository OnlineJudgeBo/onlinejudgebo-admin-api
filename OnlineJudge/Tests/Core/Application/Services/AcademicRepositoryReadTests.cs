using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

public class AcademicRepositoryReadTests
{
    private static readonly DateTime Now = DateTime.Now;

    private sealed class CourseSeed : IDisposable
    {
        public JudgeSeed Seed { get; } = new();

        public AcademicRepository Repository => new(Seed.Db, Seed.Academic);

        public long Course(long courseId = 7, string name = "Algoritmos", string? createdBy = "teacher")
        {
            Seed.Academic.Courses.Add(new DbCourse { CourseId = courseId, CourseKey = "c" + courseId, InviteCode = "INV" + courseId, Name = name, CreatedByUserId = createdBy });
            Seed.Academic.SaveChanges();
            return courseId;
        }

        public CourseSeed Member(long courseId, string userId, string role)
        {
            Seed.Academic.CourseUsers.Add(new DbCourseUser { CourseId = courseId, UserId = userId, Role = role });
            Seed.Academic.SaveChanges();
            return this;
        }

        public long Assignment(long courseId, string title, int[] problemIds, DateTime? opensAt = null, bool active = true, int[]? hiddenProblemIds = null)
        {
            var assignment = new DbCourseAssignment
            {
                CourseId = courseId, Title = title, OpensAt = opensAt ?? Now.AddDays(-1), DueAt = (opensAt ?? Now.AddDays(-1)).AddDays(7), IsActive = active, CreatedAt = Now, CreatedByUserId = "teacher"
            };
            Seed.Academic.CourseAssignments.Add(assignment);
            Seed.Academic.SaveChanges();
            foreach (var problemId in problemIds)
            {
                Seed.Academic.CourseAssignmentProblems.Add(new DbCourseAssignmentProblem { AssignmentId = assignment.AssignmentId, ProblemId = problemId, Points = 100, IsVisible = true });
            }

            foreach (var problemId in hiddenProblemIds ?? Array.Empty<int>())
            {
                Seed.Academic.CourseAssignmentProblems.Add(new DbCourseAssignmentProblem { AssignmentId = assignment.AssignmentId, ProblemId = problemId, Points = 100, IsVisible = false });
            }

            Seed.Academic.SaveChanges();
            return assignment.AssignmentId;
        }

        public int Submit(long courseId, long assignmentId, string userId, int problemId, short result, int siteId = 1)
        {
            var solutionId = Seed.Solution(userId, problemId, result, siteId: siteId);
            Seed.Academic.CourseSubmissionContexts.Add(new DbCourseSubmissionContext { SolutionId = solutionId, CourseId = courseId, AssignmentId = assignmentId, UserId = userId, CreatedAt = Now });
            Seed.Academic.SaveChanges();
            return solutionId;
        }

        public long Material(long courseId, string title, bool published, int position)
        {
            var item = new DbCourseContentItem { CourseId = courseId, ItemType = "material", Title = title, ContentBody = "x", IsPublished = published, Position = position, CreatedByUserId = "teacher", CreatedAt = Now };
            Seed.Academic.CourseContentItems.Add(item);
            Seed.Academic.SaveChanges();
            return item.ItemId;
        }

        public void Dispose() => Seed.Dispose();
    }

    [Fact]
    public async Task GetMyCourses_ReturnsOnlyMembershipsWithRole()
    {
        using var data = new CourseSeed();
        var mine = data.Course(7, "Algoritmos");
        data.Course(8, "Otro");
        data.Member(mine, "ana", CourseRoleNames.Student);

        var courses = (await data.Repository.GetMyCoursesAsync(1, "ana")).ToList();

        var course = Assert.Single(courses);
        Assert.Equal(mine, course.CourseId);
        Assert.Equal(CourseRoleNames.Student, course.Role);
        Assert.Empty(await data.Repository.GetMyCoursesAsync(1, "nobody"));
    }

    [Fact]
    public async Task GetManageableCourses_CreatorsAndStaffMembersOrAllForAdmin()
    {
        using var data = new CourseSeed();
        var created = data.Course(7, "B curso", createdBy: "teacher");
        var assisted = data.Course(8, "A curso", createdBy: "other");
        data.Course(9, "C ajeno", createdBy: "other");
        data.Member(assisted, "teacher", CourseRoleNames.Assistant).Member(9, "teacher", CourseRoleNames.Student);
        var repository = data.Repository;

        var teacherCourses = (await repository.GetManageableCoursesAsync(1, "teacher", false)).ToList();
        var adminCourses = (await repository.GetManageableCoursesAsync(1, "admin", true)).ToList();

        Assert.Equal(new[] { assisted, created }, teacherCourses.Select(item => item.CourseId));
        Assert.Equal(3, adminCourses.Count);
        Assert.Empty(await repository.GetManageableCoursesAsync(1, "nobody", false));
    }

    [Fact]
    public async Task GetCourse_RejectsNonMembersUnlessAdminAndUnknownCourse()
    {
        using var data = new CourseSeed();
        var course = data.Course();
        var repository = data.Repository;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.GetCourseAsync(1, course, "outsider", false));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetCourseAsync(1, 999, "admin", true));

        var asAdmin = await repository.GetCourseAsync(1, course, "admin", true);
        Assert.True(asAdmin.CanManage);
        Assert.Equal(CourseRoleNames.Admin, asAdmin.MemberRole);
    }

    [Fact]
    public async Task GetCourse_StudentSeesPublishedContentWithoutInviteCode()
    {
        using var data = new CourseSeed();
        var course = data.Course();
        data.Member(course, "teacher", CourseRoleNames.Teacher).Member(course, "ana", CourseRoleNames.Student).Member(course, "bob", CourseRoleNames.Student);
        data.Material(course, "Publicado", published: true, position: 20);
        data.Material(course, "Borrador", published: false, position: 10);
        var p = data.Seed.Problem("Suma");
        data.Assignment(course, "Tarea 1", new[] { p });

        var student = await data.Repository.GetCourseAsync(1, course, "ana", false);
        var teacher = await data.Repository.GetCourseAsync(1, course, "teacher", false);

        Assert.False(student.CanManage);
        Assert.Equal(string.Empty, student.InviteCode);
        Assert.Equal("teacher", student.OwnerUserId);
        Assert.Equal(2, student.StudentCount);
        Assert.Equal(1, student.AssignmentCount);
        Assert.DoesNotContain(student.Content, item => item.Title == "Borrador");
        Assert.Contains(student.Content, item => item.Type == "contest" && item.Assignment != null);
        Assert.True(teacher.CanManage);
        Assert.Equal("INV7", teacher.InviteCode);
        Assert.Equal(new[] { "Borrador", "Publicado" }, teacher.Content.Where(item => item.Type == "material").Select(item => item.Title));
    }

    [Fact]
    public async Task GetCourse_AssignmentCarriesCurrentUserProgressAndHidesInvisibleProblems()
    {
        using var data = new CourseSeed();
        var course = data.Course();
        data.Member(course, "ana", CourseRoleNames.Student).Member(course, "bob", CourseRoleNames.Student);
        var p1 = data.Seed.Problem("Suma");
        var p2 = data.Seed.Problem("Resta");
        var hidden = data.Seed.Problem("Oculto");
        var assignment = data.Assignment(course, "Tarea 1", new[] { p1, p2 }, hiddenProblemIds: new[] { hidden });
        data.Submit(course, assignment, "ana", p1, 6);
        data.Submit(course, assignment, "ana", p1, 4);
        data.Submit(course, assignment, "bob", p1, 4);

        var course7 = await data.Repository.GetCourseAsync(1, course, "ana", false);

        var item = Assert.Single(course7.Assignments);
        Assert.Equal("active", item.StatusKey);
        Assert.Equal(2, item.ProblemCount);
        Assert.Equal(2, item.AttemptsByCurrentUser);
        Assert.Equal(1, item.SolvedByCurrentUser);
        var suma = item.Problems.Single(problem => problem.ProblemId == p1);
        Assert.True(suma.IsSolvedByCurrentUser);
        Assert.Equal(3, suma.TotalSubmissions);
        Assert.DoesNotContain(item.Problems, problem => problem.ProblemId == hidden);
    }

    [Theory]
    [InlineData(false, 1, "upcoming")]
    [InlineData(true, 0, "inactive")]
    public async Task GetCourse_AssignmentStatus(bool inactive, int daysAhead, string expected)
    {
        using var data = new CourseSeed();
        var course = data.Course();
        data.Member(course, "ana", CourseRoleNames.Student);
        var p = data.Seed.Problem("Suma");
        data.Assignment(course, "Tarea", new[] { p }, opensAt: Now.AddDays(daysAhead == 0 ? -1 : daysAhead), active: !inactive);

        var status = (await data.Repository.GetCourseAsync(1, course, "ana", false)).Assignments.Single().StatusKey;

        Assert.Equal(expected, status);
    }

    [Fact]
    public async Task GetCourseMembers_SortsByRoleAndMarksOwner()
    {
        using var data = new CourseSeed();
        var course = data.Course();
        data.Seed.User("zed", nick: "Zed");
        data.Member(course, "zed", CourseRoleNames.Student).Member(course, "aux", CourseRoleNames.Assistant).Member(course, "teacher", CourseRoleNames.Teacher);

        var members = (await data.Repository.GetCourseMembersAsync(1, course)).ToList();

        Assert.Equal(new[] { "teacher", "aux", "zed" }, members.Select(member => member.UserId));
        Assert.True(members[0].IsOwner);
        Assert.Equal("Zed", members[2].Nick);
        Assert.Equal("aux", members[1].Nick);
        await Assert.ThrowsAsync<ArgumentException>(() => data.Repository.GetCourseMembersAsync(1, 999));
    }

    [Fact]
    public async Task ReorderCourseContent_AssignsSpacedPositionsAndIgnoresUnknownIds()
    {
        using var data = new CourseSeed();
        var course = data.Course();
        var a = data.Material(course, "A", true, 10);
        var b = data.Material(course, "B", true, 20);

        await data.Repository.ReorderCourseContentAsync(course, new long[] { b, 999, a });

        var positions = await data.Seed.Academic.CourseContentItems.AsNoTracking().ToDictionaryAsync(item => item.ItemId, item => item.Position);
        Assert.Equal(10, positions[b]);
        Assert.Equal(20, positions[a]);
    }

    [Fact]
    public async Task GetCourseAssignment_RanksStudentsOnly()
    {
        using var data = new CourseSeed();
        var course = data.Course();
        data.Member(course, "teacher", CourseRoleNames.Teacher).Member(course, "ana", CourseRoleNames.Student).Member(course, "bob", CourseRoleNames.Student).Member(course, "carl", CourseRoleNames.Student);
        var p1 = data.Seed.Problem("A");
        var p2 = data.Seed.Problem("B");
        var assignment = data.Assignment(course, "Tarea", new[] { p1, p2 });
        data.Submit(course, assignment, "ana", p1, 4);
        data.Submit(course, assignment, "ana", p2, 6);
        data.Submit(course, assignment, "bob", p1, 4);
        data.Submit(course, assignment, "teacher", p1, 4);

        var detail = await data.Repository.GetCourseAssignmentAsync(1, course, assignment, "ana");

        Assert.Equal(new[] { "bob", "ana", "carl" }, detail.Items.Select(item => item.UserId));
        Assert.Equal(3, detail.StudentCount);
        Assert.Equal(2, detail.ParticipantCount);
        Assert.Equal(3, detail.TotalSubmissions);
        Assert.Equal(2, detail.TotalAccepted);
        Assert.Equal(50m, detail.Items.ElementAt(1).Accuracy);
        await Assert.ThrowsAsync<ArgumentException>(() => data.Repository.GetCourseAssignmentAsync(1, course, 999, "ana"));
        await Assert.ThrowsAsync<ArgumentException>(() => data.Repository.GetCourseAssignmentAsync(1, 999, assignment, "ana"));
    }

    [Fact]
    public async Task GetCourseAssignmentSubmissions_ListsStudentSubmissionsNewestFirst()
    {
        using var data = new CourseSeed();
        var course = data.Course();
        data.Member(course, "ana", CourseRoleNames.Student).Member(course, "teacher", CourseRoleNames.Teacher);
        var p = data.Seed.Problem("Suma");
        var assignment = data.Assignment(course, "Tarea", new[] { p });
        var first = data.Submit(course, assignment, "ana", p, 6);
        var second = data.Submit(course, assignment, "ana", p, 4);
        data.Submit(course, assignment, "teacher", p, 4);

        var page = await data.Repository.GetCourseAssignmentSubmissionsAsync(1, course, assignment, 1, 10);
        var beyond = await data.Repository.GetCourseAssignmentSubmissionsAsync(1, course, assignment, 5, 10);

        Assert.Equal(2, page.Total);
        Assert.Equal(new[] { second, first }, page.Items.Select(item => item.SolutionId));
        Assert.Equal("Suma", page.Items.First().ProblemTitle);
        Assert.Empty(beyond.Items);
        await Assert.ThrowsAsync<ArgumentException>(() => data.Repository.GetCourseAssignmentSubmissionsAsync(1, course, 999, 1, 10));
    }

    [Fact]
    public async Task GetCourseAssignmentSubmissions_EmptyWhenCourseHasNoStudents()
    {
        using var data = new CourseSeed();
        var course = data.Course();
        var p = data.Seed.Problem("Suma");
        var assignment = data.Assignment(course, "Tarea", new[] { p });

        var page = await data.Repository.GetCourseAssignmentSubmissionsAsync(1, course, assignment, 1, 10);

        Assert.Equal(0, page.Total);
    }

    [Fact]
    public async Task CanViewCourseSubmissionSource_OnlyCourseStaffCreatorOrAdmin()
    {
        using var data = new CourseSeed();
        var course = data.Course(createdBy: "creator");
        data.Member(course, "aux", CourseRoleNames.Assistant).Member(course, "ana", CourseRoleNames.Student).Member(course, "bob", CourseRoleNames.Student);
        var p = data.Seed.Problem("Suma");
        var assignment = data.Assignment(course, "Tarea", new[] { p });
        var solution = data.Submit(course, assignment, "ana", p, 4);
        var practice = data.Seed.Solution("ana", p, 4);
        var otherSite = data.Submit(course, assignment, "ana", p, 4, siteId: 2);
        var repository = data.Repository;

        Assert.True(await repository.CanViewCourseSubmissionSourceAsync(1, solution, "aux", false));
        Assert.True(await repository.CanViewCourseSubmissionSourceAsync(1, solution, "CREATOR", false));
        Assert.True(await repository.CanViewCourseSubmissionSourceAsync(1, solution, "anyone", true));
        Assert.False(await repository.CanViewCourseSubmissionSourceAsync(1, solution, "bob", false));
        Assert.False(await repository.CanViewCourseSubmissionSourceAsync(1, practice, "aux", true));
        Assert.False(await repository.CanViewCourseSubmissionSourceAsync(1, otherSite, "aux", true));
    }

    [Fact]
    public async Task CourseRankingAndReport_AggregatePerAssignment()
    {
        using var data = new CourseSeed();
        var course = data.Course();
        data.Member(course, "teacher", CourseRoleNames.Teacher).Member(course, "ana", CourseRoleNames.Student).Member(course, "bob", CourseRoleNames.Student);
        var p1 = data.Seed.Problem("A");
        var p2 = data.Seed.Problem("B");
        var t1 = data.Assignment(course, "Tarea 1", new[] { p1 }, opensAt: Now.AddDays(-5));
        var t2 = data.Assignment(course, "Tarea 2", new[] { p2 }, opensAt: Now.AddDays(-2), hiddenProblemIds: new[] { p1 });
        data.Submit(course, t1, "ana", p1, 4);
        data.Submit(course, t2, "ana", p2, 4);
        data.Submit(course, t1, "bob", p1, 6);
        var repository = data.Repository;

        var ranking = (await repository.GetCourseRankingAsync(1, course)).ToList();
        var report = await repository.GetCourseReportAsync(1, course);

        Assert.Equal(new[] { "ana", "bob" }, ranking.Select(item => item.UserId));
        Assert.Equal(200m, ranking[0].TotalScore);
        Assert.Equal(new[] { "Tarea 1", "Tarea 2" }, ranking[0].Assignments.Select(item => item.Title));
        Assert.Equal("teacher", report.OwnerUserId);
        Assert.Equal(2, report.StudentCount);
        Assert.Equal(new[] { 1, 1 }, report.Assignments.Select(item => item.ProblemCount));
        var bob = report.Items.Single(item => item.UserId == "bob");
        Assert.Equal(1, bob.TotalAttempts);
        Assert.Equal(0, bob.TotalSolved);
        Assert.Equal(1, bob.Assignments.Single(cell => cell.AssignmentId == t1).Attempts);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetCourseReportAsync(1, 999));
    }

    [Fact]
    public async Task GetStudentProgress_SummarizesEveryAssignment()
    {
        using var data = new CourseSeed();
        var course = data.Course();
        data.Seed.User("ana", nick: "Ana");
        data.Member(course, "ana", CourseRoleNames.Student);
        var p1 = data.Seed.Problem("A");
        var p2 = data.Seed.Problem("B");
        var t1 = data.Assignment(course, "Tarea 1", new[] { p1 }, opensAt: Now.AddDays(-5));
        var t2 = data.Assignment(course, "Tarea 2", new[] { p2 }, opensAt: Now.AddDays(-2));
        data.Submit(course, t1, "ana", p1, 6);
        data.Submit(course, t1, "ana", p1, 4);

        var progress = await data.Repository.GetStudentProgressAsync(1, course, "ana");

        Assert.Equal("Ana", progress.Nick);
        Assert.Equal(2, progress.TotalAttempts);
        Assert.Equal(1, progress.TotalSolved);
        Assert.Equal(100m, progress.TotalScore);
        Assert.Equal(new[] { t1, t2 }, progress.Assignments.Select(item => item.AssignmentId));
        Assert.Equal(0, progress.Assignments.Last().Attempts);
    }
}
