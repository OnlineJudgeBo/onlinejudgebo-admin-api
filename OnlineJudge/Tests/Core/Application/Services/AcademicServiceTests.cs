using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;

public class AcademicServiceTests
{
    [Fact]
    public async Task GetInstitutionsAsync_RejectsCrossSiteAccess()
    {
        var service = new AcademicService(Mock.Of<IAcademicRepository>());

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetInstitutionsAsync(2, CurrentUser(UserRolesEnum.Invitado)));

        Assert.Equal("Cross-site access is forbidden.", error.Message);
    }

    [Fact]
    public async Task GetManageableCoursesAsync_RewritesAdminRoleForDocenteManagers()
    {
        var repository = new Mock<IAcademicRepository>();
        repository
            .Setup(item => item.GetManageableCoursesAsync(1, "teacher", false))
            .ReturnsAsync(new[]
            {
                new AcademicCourseSummary { CourseId = 1, Role = CourseRoleNames.Admin },
                new AcademicCourseSummary { CourseId = 2, Role = CourseRoleNames.Student }
            });
        var service = new AcademicService(repository.Object);

        var courses = (await service.GetManageableCoursesAsync(1, CurrentUser(UserRolesEnum.Docente, "teacher"))).ToList();

        Assert.Equal(CourseRoleNames.Teacher, courses[0].Role);
        Assert.Equal(CourseRoleNames.Student, courses[1].Role);
    }

    [Fact]
    public async Task CreateCourseAsync_RejectsNonAcademicManager()
    {
        var service = new AcademicService(Mock.Of<IAcademicRepository>());

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateCourseAsync(1, CurrentUser(UserRolesEnum.Invitado), new AcademicCourseCreationRequest { Name = "Curso" }));

        Assert.Equal("Only administrators, teachers or assistants can create courses.", error.Message);
    }

    [Fact]
    public async Task CreateCourseAsync_RejectsEmptyName()
    {
        var service = new AcademicService(Mock.Of<IAcademicRepository>());

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateCourseAsync(1, CurrentUser(UserRolesEnum.Docente), new AcademicCourseCreationRequest { Name = " " }));

        Assert.Equal("El nombre del curso es requerido.", error.Message);
    }

    [Fact]
    public async Task AddCourseMemberAsync_TrimsUserAndNormalizesRole()
    {
        AcademicCourseMemberCreationRequest? capturedRequest = null;
        var repository = new Mock<IAcademicRepository>();
        repository.Setup(item => item.GetCourseAsync(1, 10, "teacher", false)).ReturnsAsync(new AcademicCourseDetail { CourseId = 10, CanManage = true });
        repository
            .Setup(item => item.AddCourseMemberAsync(1, 10, It.IsAny<AcademicCourseMemberCreationRequest>()))
            .Callback<int, long, AcademicCourseMemberCreationRequest>((_, _, request) => capturedRequest = request)
            .ReturnsAsync(new AcademicCourseMember { UserId = "student" });
        var service = new AcademicService(repository.Object);

        await service.AddCourseMemberAsync(1, 10, CurrentUser(UserRolesEnum.Docente, "teacher"), new AcademicCourseMemberCreationRequest
        {
            UserId = " student ",
            Role = " auxiliar "
        });

        Assert.NotNull(capturedRequest);
        Assert.Equal("student", capturedRequest!.UserId);
        Assert.Equal(CourseRoleNames.Assistant, capturedRequest.Role);
    }

    [Fact]
    public async Task CreateCourseAssignmentAsync_NormalizesProblemIdsAndDefaultDates()
    {
        AcademicCourseAssignmentCreationRequest? capturedRequest = null;
        var repository = new Mock<IAcademicRepository>();
        repository.Setup(item => item.GetCourseAsync(1, 10, "teacher", false)).ReturnsAsync(new AcademicCourseDetail { CourseId = 10, CanManage = true });
        repository
            .Setup(item => item.CreateCourseAssignmentAsync(1, 10, "teacher", It.IsAny<AcademicCourseAssignmentCreationRequest>()))
            .Callback<int, long, string, AcademicCourseAssignmentCreationRequest>((_, _, _, request) => capturedRequest = request)
            .ReturnsAsync(new AcademicCourseAssignment { AssignmentId = 99 });
        var service = new AcademicService(repository.Object);

        var assignment = await service.CreateCourseAssignmentAsync(1, 10, CurrentUser(UserRolesEnum.Docente, "teacher"), new AcademicCourseAssignmentCreationRequest
        {
            Title = "Tarea",
            ProblemIds = new List<int> { 1000, 0, 1000, 1001 }
        });

        Assert.Equal(99, assignment.AssignmentId);
        Assert.NotNull(capturedRequest);
        Assert.Equal(new[] { 1000, 1001 }, capturedRequest!.ProblemIds);
        Assert.NotNull(capturedRequest.OpensAt);
        Assert.NotNull(capturedRequest.DueAt);
        Assert.True(capturedRequest.DueAt > capturedRequest.OpensAt);
    }

    [Fact]
    public async Task CreateCourseAssignmentAsync_RejectsDueDateBeforeOpenDate()
    {
        var repository = new Mock<IAcademicRepository>();
        repository.Setup(item => item.GetCourseAsync(1, 10, "teacher", false)).ReturnsAsync(new AcademicCourseDetail { CourseId = 10, CanManage = true });
        var service = new AcademicService(repository.Object);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateCourseAssignmentAsync(1, 10, CurrentUser(UserRolesEnum.Docente, "teacher"), new AcademicCourseAssignmentCreationRequest
        {
            Title = "Tarea",
            OpensAt = new DateTime(2024, 1, 2),
            DueAt = new DateTime(2024, 1, 1),
            ProblemIds = new List<int> { 1000 }
        }));

        Assert.Equal("La fecha límite debe ser posterior a la fecha de apertura.", error.Message);
    }

    [Fact]
    public async Task GetCourseAssignmentSubmissionsAsync_ClampsPaging()
    {
        var repository = new Mock<IAcademicRepository>();
        repository.Setup(item => item.GetCourseAsync(1, 10, "student", false)).ReturnsAsync(new AcademicCourseDetail { CourseId = 10 });
        repository.Setup(item => item.GetCourseAssignmentSubmissionsAsync(1, 10, 20, 1, 100)).ReturnsAsync(new PublicSubmissionsResponse { Page = 1, PageSize = 100 });
        var service = new AcademicService(repository.Object);

        var result = await service.GetCourseAssignmentSubmissionsAsync(1, 10, 20, -2, 500, CurrentUser(UserRolesEnum.Invitado, "student"));

        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.PageSize);
    }

    [Fact]
    public async Task AddCourseMemberAsync_RejectsTeacherWhoDoesNotOwnTheCourse()
    {
        var repository = new Mock<IAcademicRepository>();
        repository.Setup(item => item.GetCourseAsync(1, 10, "other-teacher", false)).ReturnsAsync(new AcademicCourseDetail { CourseId = 10, CanManage = false });
        var service = new AcademicService(repository.Object);

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AddCourseMemberAsync(1, 10, CurrentUser(UserRolesEnum.Docente, "other-teacher"), new AcademicCourseMemberCreationRequest
        {
            UserId = "student"
        }));

        Assert.Equal("Only the course's teachers, assistants or site administrators can add course members.", error.Message);
    }

    [Fact]
    public async Task GetStudentProgressAsync_RejectsOtherStudentWhenCurrentUserIsNotManager()
    {
        var repository = new Mock<IAcademicRepository>();
        repository.Setup(item => item.GetCourseAsync(1, 10, "student", false)).ReturnsAsync(new AcademicCourseDetail { CourseId = 10 });
        var service = new AcademicService(repository.Object);

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetStudentProgressAsync(1, 10, "other", CurrentUser(UserRolesEnum.Invitado, "student")));

        Assert.Equal("Only teachers, assistants or owner student can view this progress.", error.Message);
    }

    [Fact]
    public async Task GetStudentProgressAsync_RejectsTeacherWhoDoesNotOwnTheCourse()
    {
        var repository = new Mock<IAcademicRepository>();
        repository.Setup(item => item.GetCourseAsync(1, 10, "other-teacher", false)).ReturnsAsync(new AcademicCourseDetail { CourseId = 10, CanManage = false });
        var service = new AcademicService(repository.Object);

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetStudentProgressAsync(1, 10, "other", CurrentUser(UserRolesEnum.Docente, "other-teacher")));

        Assert.Equal("Only teachers, assistants or owner student can view this progress.", error.Message);
    }

    [Fact]
    public async Task SaveLearningPathProgressAsync_UsesEmptyRequestWhenNull()
    {
        LearningPathProgressUpdateRequest? capturedRequest = null;
        var repository = new Mock<IAcademicRepository>();
        repository
            .Setup(item => item.SaveLearningPathProgressAsync(1, "obi", "student", It.IsAny<LearningPathProgressUpdateRequest>()))
            .Callback<int, string, string, LearningPathProgressUpdateRequest>((_, _, _, request) => capturedRequest = request)
            .ReturnsAsync(new LearningPathProgressResponse { LearningPathKey = "obi" });
        var service = new AcademicService(repository.Object);

        var result = await service.SaveLearningPathProgressAsync(1, "obi", CurrentUser(UserRolesEnum.Invitado, "student"), null!);

        Assert.Equal("obi", result.LearningPathKey);
        Assert.NotNull(capturedRequest);
    }

    [Fact]
    public async Task SubmitAsync_ValidatesRequestAndDelegatesToRepository()
    {
        AcademicSubmissionRequest? capturedRequest = null;
        var repository = new Mock<IAcademicRepository>();
        repository
            .Setup(item => item.SubmitAsync(1, "student", It.IsAny<AcademicSubmissionRequest>()))
            .Callback<int, string, AcademicSubmissionRequest>((_, _, request) => capturedRequest = request)
            .ReturnsAsync(new AcademicSubmissionResponse { SolutionId = 123, LanguageId = 2 });
        var service = new AcademicService(repository.Object);

        var response = await service.SubmitAsync(CurrentUser(UserRolesEnum.Invitado, "student"), new AcademicSubmissionRequest
        {
            ProblemId = 1000,
            SourceCode = "print(42)",
            LanguageId = 2
        });

        Assert.Equal(123, response.SolutionId);
        Assert.NotNull(capturedRequest);
        Assert.Equal(1000, capturedRequest!.ProblemId);
    }

    [Fact]
    public async Task CreateLearningPathAsync_RejectsNonAdministrator()
    {
        var service = new AcademicService(Mock.Of<IAcademicRepository>());

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateLearningPathAsync(1, CurrentUser(UserRolesEnum.Docente), new LearningPathAdminUpsertRequest { Key = "cpp", Title = "C++" }));

        Assert.Equal("Only administrators can configure learning paths.", error.Message);
    }

    [Fact]
    public async Task CreateLearningPathAsync_ValidatesAndDelegates()
    {
        var request = new LearningPathAdminUpsertRequest { Key = "cpp", Title = "C++", Version = 1 };
        var repository = new Mock<IAcademicRepository>();
        repository.Setup(item => item.CreateLearningPathAsync(1, request)).ReturnsAsync(new LearningPathResponse { Track = new LearningPathTrack { Id = "cpp" } });
        var service = new AcademicService(repository.Object);

        var response = await service.CreateLearningPathAsync(1, CurrentUser(UserRolesEnum.Administrador, "admin"), request);

        Assert.Equal("cpp", response.Track.Id);
        repository.Verify(item => item.CreateLearningPathAsync(1, request), Times.Once);
    }

    [Fact]
    public async Task CreateLearningPathTopicAsync_RejectsInvalidProblemIds()
    {
        var service = new AcademicService(Mock.Of<IAcademicRepository>());
        var request = new LearningPathTopicAdminRequest { Key = "variables", Title = "Variables", ProblemIds = new List<int> { 0 } };

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateLearningPathTopicAsync(1, "cpp", 1, CurrentUser(UserRolesEnum.Administrador, "admin"), request));

        Assert.Equal("Los ids de problema deben ser positivos.", error.Message);
    }

    private static CurrentUser CurrentUser(UserRolesEnum role, string userId = "student")
    {
        return new CurrentUser
        {
            UserId = userId,
            SiteId = 1,
            Role = role
        };
    }
}
