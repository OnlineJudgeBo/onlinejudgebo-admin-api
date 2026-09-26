using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.Controllers;
using OnlineJudgeAdminApi.DataTransferObjects;
using static ControllerTestSupport;

public class AcademicControllerTests
{
    private readonly Mock<IAcademicService> _service = new();

    private AcademicController Controller(HttpContext? context = null)
    {
        context ??= AuthenticatedContext("teacher", 1, "Docente");
        return new AcademicController(_service.Object, Claims(context)).WithContext(context);
    }

    private static bool IsTeacher(CurrentUser user) => user.UserId == "teacher" && user.SiteId == 1 && user.Role == UserRolesEnum.Docente;

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new AcademicController(null!, Claims(AuthenticatedContext())));
        Assert.Throws<ArgumentNullException>(() => new AcademicController(_service.Object, null!));
    }

    [Fact]
    public async Task ReadEndpoints_PassRouteValuesAndCurrentUser()
    {
        var controller = Controller();

        Assert.IsType<OkObjectResult>(await controller.GetInstitutionsAsync(1));
        Assert.IsType<OkObjectResult>(await controller.GetInstitutionRankingAsync(1, 15));
        Assert.IsType<OkObjectResult>(await controller.GetMyCoursesAsync(1));
        Assert.IsType<OkObjectResult>(await controller.GetManageableCoursesAsync(1));
        Assert.IsType<OkObjectResult>(await controller.GetCourseAsync(1, 10));
        Assert.IsType<OkObjectResult>(await controller.GetCourseMembersAsync(1, 10));
        Assert.IsType<OkObjectResult>(await controller.GetCourseAssignmentAsync(1, 10, 3));
        Assert.IsType<OkObjectResult>(await controller.GetCourseAssignmentSubmissionsAsync(1, 10, 3, 2, 30));
        Assert.IsType<OkObjectResult>(await controller.GetCourseRankingAsync(1, 10));
        Assert.IsType<OkObjectResult>(await controller.GetCourseReportAsync(1, 10));
        Assert.IsType<OkObjectResult>(await controller.GetStudentProgressAsync(1, 10, "ana"));
        Assert.IsType<OkObjectResult>(await controller.GetLearningPathProgressAsync(1, "cpp"));

        _service.Verify(item => item.GetInstitutionsAsync(1, It.Is<CurrentUser>(u => IsTeacher(u))), Times.Once);
        _service.Verify(item => item.GetInstitutionsRankingAsync(1, It.Is<CurrentUser>(u => IsTeacher(u)), 15), Times.Once);
        _service.Verify(item => item.GetMyCoursesAsync(1, It.Is<CurrentUser>(u => IsTeacher(u))), Times.Once);
        _service.Verify(item => item.GetManageableCoursesAsync(1, It.Is<CurrentUser>(u => IsTeacher(u))), Times.Once);
        _service.Verify(item => item.GetCourseAsync(1, 10, It.Is<CurrentUser>(u => IsTeacher(u))), Times.Once);
        _service.Verify(item => item.GetCourseMembersAsync(1, 10, It.Is<CurrentUser>(u => IsTeacher(u))), Times.Once);
        _service.Verify(item => item.GetCourseAssignmentAsync(1, 10, 3, It.Is<CurrentUser>(u => IsTeacher(u))), Times.Once);
        _service.Verify(item => item.GetCourseAssignmentSubmissionsAsync(1, 10, 3, 2, 30, It.Is<CurrentUser>(u => IsTeacher(u))), Times.Once);
        _service.Verify(item => item.GetCourseRankingAsync(1, 10, It.Is<CurrentUser>(u => IsTeacher(u))), Times.Once);
        _service.Verify(item => item.GetCourseReportAsync(1, 10, It.Is<CurrentUser>(u => IsTeacher(u))), Times.Once);
        _service.Verify(item => item.GetStudentProgressAsync(1, 10, "ana", It.Is<CurrentUser>(u => IsTeacher(u))), Times.Once);
        _service.Verify(item => item.GetLearningPathProgressAsync(1, "cpp", It.Is<CurrentUser>(u => IsTeacher(u))), Times.Once);
    }

    [Fact]
    public async Task CreateCourse_MapsDto()
    {
        await Controller().CreateCourseAsync(1, new AcademicCourseForCreation { Name = "Algoritmos", Description = "d", InstitutionId = 4 });

        _service.Verify(item => item.CreateCourseAsync(1, It.IsAny<CurrentUser>(), It.Is<AcademicCourseCreationRequest>(r =>
            r.Name == "Algoritmos" && r.Description == "d" && r.InstitutionId == 4)), Times.Once);
    }

    [Fact]
    public async Task JoinCourse_MapsInviteCode()
    {
        await Controller().JoinCourseAsync(1, new AcademicJoinCourseForCreation { InviteCode = "ABC123" });

        _service.Verify(item => item.JoinCourseAsync(1, It.IsAny<CurrentUser>(), It.Is<AcademicJoinCourseRequest>(r => r.InviteCode == "ABC123")), Times.Once);
    }

    [Fact]
    public async Task Members_AddMapsDtoAndRemoveReturnsNoContent()
    {
        var controller = Controller();

        await controller.AddCourseMemberAsync(1, 10, new AcademicCourseMemberForCreation { UserId = "ana", Role = CourseRoleNames.Assistant });
        var removed = await controller.RemoveCourseMemberAsync(1, 10, "ana");

        Assert.IsType<NoContentResult>(removed);
        _service.Verify(item => item.AddCourseMemberAsync(1, 10, It.IsAny<CurrentUser>(), It.Is<AcademicCourseMemberCreationRequest>(r => r.UserId == "ana" && r.Role == CourseRoleNames.Assistant)), Times.Once);
        _service.Verify(item => item.RemoveCourseMemberAsync(1, 10, "ana", It.IsAny<CurrentUser>()), Times.Once);
    }

    [Fact]
    public async Task Assignments_CreateAndUpdateMapAllFields()
    {
        var dto = new AcademicAssignmentForCreation
        {
            Title = "Tarea 1",
            Description = "d",
            OpensAt = new DateTime(2026, 1, 1),
            DueAt = new DateTime(2026, 1, 8),
            LateDueAt = new DateTime(2026, 1, 9),
            IsActive = false,
            ProblemIds = new List<int> { 1000, 1001 }
        };
        Func<AcademicCourseAssignmentCreationRequest, bool> matches = r =>
            r.Title == "Tarea 1" && r.Description == "d" && r.OpensAt == dto.OpensAt && r.DueAt == dto.DueAt
            && r.LateDueAt == dto.LateDueAt && !r.IsActive && r.ProblemIds.SequenceEqual(new[] { 1000, 1001 });
        var controller = Controller();

        await controller.CreateCourseAssignmentAsync(1, 10, dto);
        await controller.UpdateCourseAssignmentAsync(1, 10, 3, dto);

        _service.Verify(item => item.CreateCourseAssignmentAsync(1, 10, It.IsAny<CurrentUser>(), It.Is<AcademicCourseAssignmentCreationRequest>(r => matches(r))), Times.Once);
        _service.Verify(item => item.UpdateCourseAssignmentAsync(1, 10, 3, It.IsAny<CurrentUser>(), It.Is<AcademicCourseAssignmentCreationRequest>(r => matches(r))), Times.Once);
    }

    [Fact]
    public async Task Materials_CreateUpdateDeleteMapFields()
    {
        var dto = new AcademicCourseMaterialForCreation { Title = "Guía", Description = "d", ContentUrl = "https://x", ContentBody = "b", IsPublished = false };
        Func<AcademicCourseMaterialCreationRequest, bool> matches = r =>
            r.Title == "Guía" && r.Description == "d" && r.ContentUrl == "https://x" && r.ContentBody == "b" && !r.IsPublished;
        var controller = Controller();

        await controller.CreateCourseMaterialAsync(1, 10, dto);
        await controller.UpdateCourseMaterialAsync(1, 10, 5, dto);
        var deleted = await controller.DeleteCourseMaterialAsync(1, 10, 5);

        Assert.IsType<NoContentResult>(deleted);
        _service.Verify(item => item.CreateCourseMaterialAsync(1, 10, It.IsAny<CurrentUser>(), It.Is<AcademicCourseMaterialCreationRequest>(r => matches(r))), Times.Once);
        _service.Verify(item => item.UpdateCourseMaterialAsync(1, 10, 5, It.IsAny<CurrentUser>(), It.Is<AcademicCourseMaterialCreationRequest>(r => matches(r))), Times.Once);
        _service.Verify(item => item.DeleteCourseMaterialAsync(1, 10, 5, It.IsAny<CurrentUser>()), Times.Once);
    }

    [Fact]
    public async Task ReorderContent_PassesIdsAndDefaultsNullToEmpty()
    {
        var controller = Controller();

        Assert.IsType<NoContentResult>(await controller.ReorderCourseContentAsync(1, 10, new AcademicCourseContentOrderForUpdate { ItemIds = new List<long> { 3, 1 } }));
        await controller.ReorderCourseContentAsync(1, 10, new AcademicCourseContentOrderForUpdate { ItemIds = null! });

        _service.Verify(item => item.ReorderCourseContentAsync(1, 10, It.IsAny<CurrentUser>(), It.Is<IReadOnlyList<long>>(ids => ids.SequenceEqual(new long[] { 3, 1 }))), Times.Once);
        _service.Verify(item => item.ReorderCourseContentAsync(1, 10, It.IsAny<CurrentUser>(), It.Is<IReadOnlyList<long>>(ids => ids.Count == 0)), Times.Once);
    }

    [Fact]
    public async Task DownloadReportCsv_ForbiddenWhenNotAllowed()
    {
        _service.Setup(item => item.GetCourseReportAsync(1, 10, It.IsAny<CurrentUser>())).ReturnsAsync(new AcademicCourseReportResponse { CanDownloadCsv = false });

        Assert.IsType<ForbidResult>(await Controller().DownloadCourseReportCsvAsync(1, 10));
    }

    [Fact]
    public async Task DownloadReportCsv_BuildsGroupedHeadersAndEscapesValues()
    {
        _service.Setup(item => item.GetCourseReportAsync(1, 10, It.IsAny<CurrentUser>())).ReturnsAsync(new AcademicCourseReportResponse
        {
            CanDownloadCsv = true,
            Assignments = new List<AcademicCourseReportAssignmentColumn>
            {
                new() { AssignmentId = 1, Title = "Tarea 1: Árboles, BFS" },
                new() { AssignmentId = 2, Title = "  ¡¡!!  " },
            },
            Items = new List<AcademicCourseReportItem>
            {
                new()
                {
                    Rank = 1, UserId = "ana", Nick = "Ana \"la\" Pro", TotalSolved = 3, TotalAttempts = 5, TotalAccepted = 3,
                    Assignments = new List<AcademicCourseReportAssignmentCell> { new() { AssignmentId = 1, Solved = 2, Attempts = 4, Accepted = 2 } }
                },
                new() { Rank = 2, UserId = "bob", Nick = "", TotalSolved = 0, TotalAttempts = 0, TotalAccepted = 0 },
            }
        });

        var file = Assert.IsType<FileContentResult>(await Controller().DownloadCourseReportCsvAsync(1, 10));
        var lines = Encoding.UTF8.GetString(file.FileContents).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.Equal("course-10-report.csv", file.FileDownloadName);
        Assert.Equal(",,,,,,\"Tarea 1: Árboles, BFS\",,,  ¡¡!!  ,,", lines[0]);
        Assert.Equal("rank,userId,nick,totalSolved,totalAttempts,totalAccepted,tarea_1_árboles_bfs_solved,tarea_1_árboles_bfs_attempts,tarea_1_árboles_bfs_accepted,assignment_solved,assignment_attempts,assignment_accepted", lines[1]);
        Assert.Equal("1,ana,\"Ana \"\"la\"\" Pro\",3,5,3,2,4,2,0,0,0", lines[2]);
        Assert.Equal("2,bob,,0,0,0,0,0,0,0,0,0", lines[3]);
    }

    [Fact]
    public async Task LearningPaths_AnonymousGetsPublicGuestContext()
    {
        var controller = Controller(new DefaultHttpContext());

        await controller.GetLearningPathsAsync(3);
        await controller.GetLearningPathAsync(3, "cpp");

        _service.Verify(item => item.GetLearningPathsAsync(3, It.Is<CurrentUser>(u => u.UserId == "public" && u.SiteId == 3 && u.Role == UserRolesEnum.Invitado)), Times.Once);
        _service.Verify(item => item.GetLearningPathAsync(3, "cpp", It.Is<CurrentUser>(u => u.UserId == "public" && u.SiteId == 3)), Times.Once);
    }

    [Fact]
    public async Task LearningPaths_AuthenticatedUsesTokenUser()
    {
        var controller = Controller();

        await controller.GetLearningPathsAsync(1);
        await controller.GetLearningPathAsync(1, "cpp");

        _service.Verify(item => item.GetLearningPathsAsync(1, It.Is<CurrentUser>(u => IsTeacher(u))), Times.Once);
        _service.Verify(item => item.GetLearningPathAsync(1, "cpp", It.Is<CurrentUser>(u => IsTeacher(u))), Times.Once);
    }

    [Fact]
    public async Task LearningPathAdministration_UsesSiteFromToken()
    {
        var controller = Controller(AuthenticatedContext("admin", 7, "Administrador"));
        var path = new LearningPathAdminUpsertRequest { Key = "cpp", Title = "C++" };
        var stage = new LearningPathStageAdminRequest { Key = "b", Name = "B", Order = 1 };
        var topic = new LearningPathTopicAdminRequest { Key = "t", Title = "T" };

        Assert.IsType<OkObjectResult>(await controller.CreateLearningPathAsync(path));
        Assert.IsType<OkObjectResult>(await controller.UpdateLearningPathAsync("cpp", path));
        Assert.IsType<NoContentResult>(await controller.DeleteLearningPathAsync("cpp"));
        Assert.IsType<OkObjectResult>(await controller.CreateLearningPathStageAsync("cpp", stage));
        Assert.IsType<OkObjectResult>(await controller.LinkLearningPathStageAsync("cpp", 4));
        Assert.IsType<NoContentResult>(await controller.UnlinkLearningPathStageAsync("cpp", 4));
        Assert.IsType<OkObjectResult>(await controller.UpdateLearningPathStageAsync("cpp", 4, stage));
        Assert.IsType<NoContentResult>(await controller.DeleteLearningPathStageAsync("cpp", 4));
        Assert.IsType<OkObjectResult>(await controller.CreateLearningPathTopicAsync("cpp", 4, topic));
        Assert.IsType<OkObjectResult>(await controller.UpdateLearningPathTopicAsync("cpp", 4, 9, topic));
        Assert.IsType<NoContentResult>(await controller.DeleteLearningPathTopicAsync("cpp", 4, 9));

        _service.Verify(item => item.CreateLearningPathAsync(7, It.IsAny<CurrentUser>(), path), Times.Once);
        _service.Verify(item => item.UpdateLearningPathAsync(7, "cpp", It.IsAny<CurrentUser>(), path), Times.Once);
        _service.Verify(item => item.DeleteLearningPathAsync(7, "cpp", It.IsAny<CurrentUser>()), Times.Once);
        _service.Verify(item => item.CreateLearningPathStageAsync(7, "cpp", It.IsAny<CurrentUser>(), stage), Times.Once);
        _service.Verify(item => item.LinkLearningPathStageAsync(7, "cpp", 4, It.IsAny<CurrentUser>()), Times.Once);
        _service.Verify(item => item.UnlinkLearningPathStageAsync(7, "cpp", 4, It.IsAny<CurrentUser>()), Times.Once);
        _service.Verify(item => item.UpdateLearningPathStageAsync(7, "cpp", 4, It.IsAny<CurrentUser>(), stage), Times.Once);
        _service.Verify(item => item.DeleteLearningPathStageAsync(7, "cpp", 4, It.IsAny<CurrentUser>()), Times.Once);
        _service.Verify(item => item.CreateLearningPathTopicAsync(7, "cpp", 4, It.IsAny<CurrentUser>(), topic), Times.Once);
        _service.Verify(item => item.UpdateLearningPathTopicAsync(7, "cpp", 4, 9, It.IsAny<CurrentUser>(), topic), Times.Once);
        _service.Verify(item => item.DeleteLearningPathTopicAsync(7, "cpp", 4, 9, It.IsAny<CurrentUser>()), Times.Once);
    }

    [Fact]
    public async Task SaveProgress_MapsDtoAndToleratesNullBody()
    {
        var controller = Controller();

        await controller.SaveLearningPathProgressAsync(1, "cpp", new AcademicLearningPathProgressForUpdate { LastTopicId = "vars", CompletedTopicIds = new List<string> { "intro" } });
        await controller.SaveLearningPathProgressAsync(1, "cpp", null!);

        _service.Verify(item => item.SaveLearningPathProgressAsync(1, "cpp", It.IsAny<CurrentUser>(), It.Is<LearningPathProgressUpdateRequest>(r => r.LastTopicId == "vars" && r.CompletedTopicIds.Single() == "intro")), Times.Once);
        _service.Verify(item => item.SaveLearningPathProgressAsync(1, "cpp", It.IsAny<CurrentUser>(), It.Is<LearningPathProgressUpdateRequest>(r => r.LastTopicId == null && r.CompletedTopicIds.Count == 0)), Times.Once);
    }

    [Fact]
    public async Task AuthenticatedEndpoints_RejectAnonymousContext()
    {
        var controller = Controller(new DefaultHttpContext());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.GetMyCoursesAsync(1));
        _service.VerifyNoOtherCalls();
    }
}
