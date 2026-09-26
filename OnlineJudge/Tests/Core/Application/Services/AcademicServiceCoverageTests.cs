using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;

public class AcademicServiceCoverageTests
{
    private readonly Mock<IAcademicRepository> _repository = new();
    private readonly AcademicService _service;

    public AcademicServiceCoverageTests()
    {
        _service = new AcademicService(_repository.Object);
    }

    [Fact]
    public void Constructor_RejectsNullRepository()
    {
        Assert.Throws<ArgumentNullException>(() => new AcademicService(null!));
    }

    [Fact]
    public async Task GetInstitutionsAsync_DelegatesForSameSite()
    {
        var institutions = new[] { new AcademicInstitution() };
        _repository.Setup(item => item.GetInstitutionsAsync(1)).ReturnsAsync(institutions);

        Assert.Same(institutions, await _service.GetInstitutionsAsync(1, User(UserRolesEnum.Invitado)));
    }

    [Fact]
    public async Task GetInstitutionsRankingAsync_PassesLimitAndRejectsCrossSite()
    {
        await _service.GetInstitutionsRankingAsync(1, User(UserRolesEnum.Invitado), 25);

        _repository.Verify(item => item.GetInstitutionsRankingAsync(1, 25), Times.Once);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetInstitutionsRankingAsync(2, User(UserRolesEnum.Invitado), 25));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("defaultUserId")]
    [InlineData("DEFAULTUSERID")]
    public async Task UserScopedOperations_RejectInvalidUserContext(string userId)
    {
        var user = User(UserRolesEnum.Docente, userId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetMyCoursesAsync(1, user));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.SubmitAsync(user, new AcademicSubmissionRequest { ProblemId = 1, SourceCode = "x" }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetCourseAsync(1, 1, user));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetMyCoursesAsync_UsesCurrentUser()
    {
        await _service.GetMyCoursesAsync(1, User(UserRolesEnum.Invitado, "ana"));

        _repository.Verify(item => item.GetMyCoursesAsync(1, "ana"), Times.Once);
    }

    [Fact]
    public async Task GetManageableCoursesAsync_AdministratorSeesAllCoursesWithoutRoleRewrite()
    {
        _repository
            .Setup(item => item.GetManageableCoursesAsync(1, "admin", true))
            .ReturnsAsync(new[] { new AcademicCourseSummary { Role = CourseRoleNames.Admin } });

        var courses = (await _service.GetManageableCoursesAsync(1, User(UserRolesEnum.Administrador, "admin"))).ToList();

        Assert.Equal(CourseRoleNames.Admin, courses[0].Role);
    }

    [Fact]
    public async Task GetManageableCoursesAsync_RewritesAdminRoleToAssistantForAuxiliar()
    {
        _repository
            .Setup(item => item.GetManageableCoursesAsync(1, "aux", false))
            .ReturnsAsync(new[] { new AcademicCourseSummary { Role = "ADMINISTRADOR" } });

        var courses = (await _service.GetManageableCoursesAsync(1, User(UserRolesEnum.Auxiliar, "aux"))).ToList();

        Assert.Equal(CourseRoleNames.Assistant, courses[0].Role);
    }

    [Fact]
    public async Task GetManageableCoursesAsync_LeavesRolesUntouchedForGuests()
    {
        _repository
            .Setup(item => item.GetManageableCoursesAsync(1, "student", false))
            .ReturnsAsync(new[] { new AcademicCourseSummary { Role = CourseRoleNames.Admin } });

        var courses = (await _service.GetManageableCoursesAsync(1, User(UserRolesEnum.Invitado))).ToList();

        Assert.Equal(CourseRoleNames.Admin, courses[0].Role);
    }

    [Theory]
    [InlineData(UserRolesEnum.Administrador)]
    [InlineData(UserRolesEnum.Docente)]
    [InlineData(UserRolesEnum.Auxiliar)]
    public async Task CreateCourseAsync_AllowsAcademicManagers(UserRolesEnum role)
    {
        var request = new AcademicCourseCreationRequest { Name = "Curso" };

        await _service.CreateCourseAsync(1, User(role, "owner"), request);

        _repository.Verify(item => item.CreateCourseAsync(1, "owner", request), Times.Once);
    }

    [Fact]
    public async Task JoinCourseAsync_RequiresInviteCodeAndDelegates()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => _service.JoinCourseAsync(1, User(UserRolesEnum.Invitado), new AcademicJoinCourseRequest { InviteCode = " " }));
        Assert.Equal("El código de invitación es requerido.", error.Message);

        var request = new AcademicJoinCourseRequest { InviteCode = "ABC" };
        await _service.JoinCourseAsync(1, User(UserRolesEnum.Invitado), request);
        _repository.Verify(item => item.JoinCourseAsync(1, "student", request), Times.Once);
    }

    [Fact]
    public async Task GetCourseAsync_AdministratorCanManageAndGetsAdminRoleWhenNotMember()
    {
        SetupCourse(new AcademicCourseDetail { CanManage = false, MemberRole = null }, isAdmin: true, userId: "admin");

        var course = await _service.GetCourseAsync(1, 10, User(UserRolesEnum.Administrador, "admin"));

        Assert.True(course.CanManage);
        Assert.Equal(CourseRoleNames.Admin, course.MemberRole);
    }

    [Fact]
    public async Task GetCourseAsync_AdministratorKeepsExistingMemberRole()
    {
        SetupCourse(new AcademicCourseDetail { MemberRole = CourseRoleNames.Teacher }, isAdmin: true, userId: "admin");

        var course = await _service.GetCourseAsync(1, 10, User(UserRolesEnum.Administrador, "admin"));

        Assert.Equal(CourseRoleNames.Teacher, course.MemberRole);
    }

    [Fact]
    public async Task GetCourseAsync_NonAdministratorGetsRepositoryValues()
    {
        SetupCourse(new AcademicCourseDetail { CanManage = false, MemberRole = CourseRoleNames.Student });

        var course = await _service.GetCourseAsync(1, 10, User(UserRolesEnum.Invitado));

        Assert.False(course.CanManage);
        Assert.Equal(CourseRoleNames.Student, course.MemberRole);
    }

    [Fact]
    public async Task GetCourseMembersAsync_RequiresManager()
    {
        SetupCourse(new AcademicCourseDetail { CanManage = false });

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetCourseMembersAsync(1, 10, User(UserRolesEnum.Invitado)));
        Assert.Contains("view course members", error.Message);
        _repository.Verify(item => item.GetCourseMembersAsync(It.IsAny<int>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task GetCourseMembersAsync_DelegatesForManager()
    {
        SetupCourse(new AcademicCourseDetail { CanManage = true });

        await _service.GetCourseMembersAsync(1, 10, User(UserRolesEnum.Invitado));

        _repository.Verify(item => item.GetCourseMembersAsync(1, 10), Times.Once);
    }

    [Fact]
    public async Task AddCourseMemberAsync_RequiresUserId()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => _service.AddCourseMemberAsync(1, 10, User(UserRolesEnum.Docente), new AcademicCourseMemberCreationRequest { UserId = " " }));

        Assert.Equal("El nombre de usuario del miembro es requerido.", error.Message);
    }

    [Theory]
    [InlineData(null, CourseRoleNames.Student)]
    [InlineData("docente", CourseRoleNames.Student)]
    [InlineData(" AUXILIAR ", CourseRoleNames.Assistant)]
    public async Task AddCourseMemberAsync_OnlyAllowsStudentOrAssistantRoles(string? requestedRole, string expectedRole)
    {
        SetupCourse(new AcademicCourseDetail { CanManage = true });
        var request = new AcademicCourseMemberCreationRequest { UserId = "bob", Role = requestedRole };

        await _service.AddCourseMemberAsync(1, 10, User(UserRolesEnum.Docente), request);

        Assert.Equal(expectedRole, request.Role);
    }

    [Fact]
    public async Task RemoveCourseMemberAsync_ValidatesPermissionsAndTrimsUser()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.RemoveCourseMemberAsync(1, 10, " ", User(UserRolesEnum.Docente)));

        SetupCourse(new AcademicCourseDetail { CanManage = false });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.RemoveCourseMemberAsync(1, 10, "bob", User(UserRolesEnum.Docente)));

        SetupCourse(new AcademicCourseDetail { CanManage = true });
        await _service.RemoveCourseMemberAsync(1, 10, " bob ", User(UserRolesEnum.Docente));
        _repository.Verify(item => item.RemoveCourseMemberAsync(1, 10, "bob"), Times.Once);
    }

    [Fact]
    public async Task CreateCourseAssignmentAsync_RequiresManagerAndTitle()
    {
        SetupCourse(new AcademicCourseDetail { CanManage = false });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateCourseAssignmentAsync(1, 10, User(UserRolesEnum.Docente), Assignment()));

        SetupCourse(new AcademicCourseDetail { CanManage = true });
        var error = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateCourseAssignmentAsync(1, 10, User(UserRolesEnum.Docente), Assignment(title: " ")));
        Assert.Equal("El título de la tarea es requerido.", error.Message);
    }

    [Fact]
    public async Task CreateCourseAssignmentAsync_RequiresAtLeastOnePositiveProblem()
    {
        SetupCourse(new AcademicCourseDetail { CanManage = true });

        var error = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateCourseAssignmentAsync(1, 10, User(UserRolesEnum.Docente), Assignment(problemIds: new List<int> { 0, -3 })));

        Assert.Equal("Se requiere al menos un problema.", error.Message);
    }

    [Fact]
    public async Task CreateCourseAssignmentAsync_RejectsLateDueBeforeDue()
    {
        SetupCourse(new AcademicCourseDetail { CanManage = true });
        var request = Assignment();
        request.OpensAt = new DateTime(2026, 1, 1);
        request.DueAt = new DateTime(2026, 1, 5);
        request.LateDueAt = new DateTime(2026, 1, 4);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateCourseAssignmentAsync(1, 10, User(UserRolesEnum.Docente), request));

        Assert.Equal("La fecha límite tardía debe ser posterior a la fecha límite.", error.Message);
    }

    [Fact]
    public async Task CreateCourseAssignmentAsync_KeepsExplicitDatesAndPassesCreator()
    {
        SetupCourse(new AcademicCourseDetail { CanManage = true }, userId: "teacher");
        var request = Assignment();
        request.OpensAt = new DateTime(2026, 1, 1);
        request.DueAt = new DateTime(2026, 1, 5);
        request.LateDueAt = new DateTime(2026, 1, 6);

        await _service.CreateCourseAssignmentAsync(1, 10, User(UserRolesEnum.Docente, "teacher"), request);

        Assert.Equal(new DateTime(2026, 1, 5), request.DueAt);
        _repository.Verify(item => item.CreateCourseAssignmentAsync(1, 10, "teacher", request), Times.Once);
    }

    [Fact]
    public async Task CreateCourseMaterialAsync_DelegatesValidMaterial()
    {
        SetupCourse(new AcademicCourseDetail { CanManage = true }, userId: "teacher");
        var request = new AcademicCourseMaterialCreationRequest { Title = "Guía", ContentUrl = "https://example.com/guia.pdf" };

        await _service.CreateCourseMaterialAsync(1, 10, User(UserRolesEnum.Docente, "teacher"), request);

        _repository.Verify(item => item.CreateCourseMaterialAsync(10, "teacher", request), Times.Once);
    }

    [Theory]
    [InlineData(" ", "texto", null, "El título del material es requerido.")]
    [InlineData("Guía", null, null, "Agrega contenido del material o un enlace de recurso.")]
    [InlineData("Guía", " ", " ", "Agrega contenido del material o un enlace de recurso.")]
    [InlineData("Guía", null, "ftp://example.com/a", "La URL del material debe ser una dirección HTTP o HTTPS válida.")]
    [InlineData("Guía", null, "javascript:alert(1)", "La URL del material debe ser una dirección HTTP o HTTPS válida.")]
    [InlineData("Guía", null, "not a url", "La URL del material debe ser una dirección HTTP o HTTPS válida.")]
    public async Task CreateCourseMaterialAsync_RejectsInvalidMaterial(string title, string? body, string? url, string expectedMessage)
    {
        SetupCourse(new AcademicCourseDetail { CanManage = true });
        var request = new AcademicCourseMaterialCreationRequest { Title = title, ContentBody = body, ContentUrl = url };

        var error = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateCourseMaterialAsync(1, 10, User(UserRolesEnum.Docente), request));

        Assert.Equal(expectedMessage, error.Message);
    }

    [Fact]
    public async Task CreateCourseMaterialAsync_AcceptsBodyWithoutUrl()
    {
        SetupCourse(new AcademicCourseDetail { CanManage = true });

        await _service.CreateCourseMaterialAsync(1, 10, User(UserRolesEnum.Docente), new AcademicCourseMaterialCreationRequest { Title = "Nota", ContentBody = "texto" });

        _repository.Verify(item => item.CreateCourseMaterialAsync(10, "student", It.IsAny<AcademicCourseMaterialCreationRequest>()), Times.Once);
    }

    [Fact]
    public async Task CreateCourseMaterialAsync_RequiresManager()
    {
        SetupCourse(new AcademicCourseDetail { CanManage = false });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateCourseMaterialAsync(1, 10, User(UserRolesEnum.Invitado), new AcademicCourseMaterialCreationRequest { Title = "Nota", ContentBody = "x" }));
    }

    [Fact]
    public async Task UpdateCourseMaterialAsync_ValidatesIdPermissionsAndContent()
    {
        var valid = new AcademicCourseMaterialCreationRequest { Title = "Nota", ContentBody = "x" };
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateCourseMaterialAsync(1, 10, 0, User(UserRolesEnum.Docente), valid));

        SetupCourse(new AcademicCourseDetail { CanManage = false });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.UpdateCourseMaterialAsync(1, 10, 5, User(UserRolesEnum.Docente), valid));

        SetupCourse(new AcademicCourseDetail { CanManage = true });
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateCourseMaterialAsync(1, 10, 5, User(UserRolesEnum.Docente), new AcademicCourseMaterialCreationRequest { Title = "Nota" }));

        await _service.UpdateCourseMaterialAsync(1, 10, 5, User(UserRolesEnum.Docente), valid);
        _repository.Verify(item => item.UpdateCourseMaterialAsync(10, 5, valid), Times.Once);
    }

    [Fact]
    public async Task DeleteCourseMaterialAsync_ValidatesIdAndPermissions()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.DeleteCourseMaterialAsync(1, 10, -1, User(UserRolesEnum.Docente)));

        SetupCourse(new AcademicCourseDetail { CanManage = false });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.DeleteCourseMaterialAsync(1, 10, 5, User(UserRolesEnum.Docente)));

        SetupCourse(new AcademicCourseDetail { CanManage = true });
        await _service.DeleteCourseMaterialAsync(1, 10, 5, User(UserRolesEnum.Docente));
        _repository.Verify(item => item.DeleteCourseMaterialAsync(10, 5), Times.Once);
    }

    [Fact]
    public async Task ReorderCourseContentAsync_AcceptsExactPermutation()
    {
        SetupCourse(CourseWithContent(1, 2, 3));

        await _service.ReorderCourseContentAsync(1, 10, User(UserRolesEnum.Docente), new long[] { 3, 1, 2 });

        _repository.Verify(item => item.ReorderCourseContentAsync(10, It.Is<IReadOnlyList<long>>(ids => ids.SequenceEqual(new long[] { 3, 1, 2 }))), Times.Once);
    }

    public static IEnumerable<object?[]> InvalidOrders => new[]
    {
        new object?[] { null },
        new object?[] { Array.Empty<long>() },
        new object?[] { new long[] { 1, 2 } },
        new object?[] { new long[] { 1, 2, 2 } },
        new object?[] { new long[] { 1, 2, 9 } },
        new object?[] { new long[] { 1, 2, 3, 4 } },
    };

    [Theory]
    [MemberData(nameof(InvalidOrders))]
    public async Task ReorderCourseContentAsync_RejectsOrdersThatDoNotMatchContent(long[]? itemIds)
    {
        SetupCourse(CourseWithContent(1, 2, 3));

        var error = await Assert.ThrowsAsync<ArgumentException>(() => _service.ReorderCourseContentAsync(1, 10, User(UserRolesEnum.Docente), itemIds!));

        Assert.Equal("El orden proporcionado no coincide con el contenido del curso.", error.Message);
        _repository.Verify(item => item.ReorderCourseContentAsync(It.IsAny<long>(), It.IsAny<IReadOnlyList<long>>()), Times.Never);
    }

    [Fact]
    public async Task ReorderCourseContentAsync_RequiresManager()
    {
        var course = CourseWithContent(1);
        course.CanManage = false;
        SetupCourse(course);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.ReorderCourseContentAsync(1, 10, User(UserRolesEnum.Invitado), new long[] { 1 }));
    }

    [Fact]
    public async Task UpdateCourseAssignmentAsync_ValidatesAndDelegates()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateCourseAssignmentAsync(1, 10, 0, User(UserRolesEnum.Docente), Assignment()));

        SetupCourse(new AcademicCourseDetail { CanManage = false });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.UpdateCourseAssignmentAsync(1, 10, 3, User(UserRolesEnum.Docente), Assignment()));

        SetupCourse(new AcademicCourseDetail { CanManage = true }, userId: "teacher");
        var request = Assignment(problemIds: new List<int> { 5, 5, 7 });
        await _service.UpdateCourseAssignmentAsync(1, 10, 3, User(UserRolesEnum.Docente, "teacher"), request);

        Assert.Equal(new List<int> { 5, 7 }, request.ProblemIds);
        _repository.Verify(item => item.UpdateCourseAssignmentAsync(1, 10, 3, "teacher", request), Times.Once);
    }

    [Fact]
    public async Task GetCourseAssignmentAsync_RejectsInvalidId()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetCourseAssignmentAsync(1, 10, 0, User(UserRolesEnum.Invitado)));
    }

    [Fact]
    public async Task GetCourseAssignmentAsync_CopiesCourseRoleAndPermissions()
    {
        SetupCourse(new AcademicCourseDetail { CanManage = false, MemberRole = CourseRoleNames.Student });
        _repository.Setup(item => item.GetCourseAssignmentAsync(1, 10, 3, "student")).ReturnsAsync(new AcademicCourseAssignmentDetailResponse());

        var detail = await _service.GetCourseAssignmentAsync(1, 10, 3, User(UserRolesEnum.Invitado));

        Assert.Equal(CourseRoleNames.Student, detail.MemberRole);
        Assert.False(detail.CanManage);
    }

    [Fact]
    public async Task GetCourseAssignmentAsync_ManagerWithoutMembershipGetsManagerRole()
    {
        SetupCourse(new AcademicCourseDetail { CanManage = true, MemberRole = null });
        _repository.Setup(item => item.GetCourseAssignmentAsync(1, 10, 3, "student")).ReturnsAsync(new AcademicCourseAssignmentDetailResponse());

        var detail = await _service.GetCourseAssignmentAsync(1, 10, 3, User(UserRolesEnum.Docente));

        Assert.Equal(CourseRoleNames.Teacher, detail.MemberRole);
        Assert.True(detail.CanManage);
    }

    [Fact]
    public async Task GetCourseAssignmentSubmissionsAsync_RejectsInvalidIdAndClampsUpperPageSize()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetCourseAssignmentSubmissionsAsync(1, 10, 0, 1, 10, User(UserRolesEnum.Invitado)));

        SetupCourse(new AcademicCourseDetail());
        await _service.GetCourseAssignmentSubmissionsAsync(1, 10, 3, 4, 500, User(UserRolesEnum.Invitado));
        _repository.Verify(item => item.GetCourseAssignmentSubmissionsAsync(1, 10, 3, 4, 100), Times.Once);
    }

    [Fact]
    public async Task GetCourseRankingAsync_ChecksCourseAccessFirst()
    {
        SetupCourse(new AcademicCourseDetail());

        await _service.GetCourseRankingAsync(1, 10, User(UserRolesEnum.Invitado));

        _repository.Verify(item => item.GetCourseAsync(1, 10, "student", false), Times.Once);
        _repository.Verify(item => item.GetCourseRankingAsync(1, 10), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetCourseReportAsync_CsvOnlyForManagers(bool canManage)
    {
        SetupCourse(new AcademicCourseDetail { CanManage = canManage });
        _repository.Setup(item => item.GetCourseReportAsync(1, 10)).ReturnsAsync(new AcademicCourseReportResponse());

        var report = await _service.GetCourseReportAsync(1, 10, User(UserRolesEnum.Invitado));

        Assert.Equal(canManage, report.CanDownloadCsv);
    }

    [Fact]
    public async Task GetStudentProgressAsync_AllowsOwnProgressCaseInsensitive()
    {
        SetupCourse(new AcademicCourseDetail { CanManage = false }, userId: "Ana");

        await _service.GetStudentProgressAsync(1, 10, "ana", User(UserRolesEnum.Invitado, "Ana"));

        _repository.Verify(item => item.GetStudentProgressAsync(1, 10, "ana"), Times.Once);
    }

    [Fact]
    public async Task GetStudentProgressAsync_AllowsManagerToViewAnyStudent()
    {
        SetupCourse(new AcademicCourseDetail { CanManage = true }, userId: "teacher");

        await _service.GetStudentProgressAsync(1, 10, "bob", User(UserRolesEnum.Docente, "teacher"));

        _repository.Verify(item => item.GetStudentProgressAsync(1, 10, "bob"), Times.Once);
    }

    [Fact]
    public async Task GetLearningPathsAsync_ValidatesSite()
    {
        await _service.GetLearningPathsAsync(1, User(UserRolesEnum.Invitado));
        _repository.Verify(item => item.GetLearningPathsAsync(1), Times.Once);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetLearningPathsAsync(3, User(UserRolesEnum.Invitado)));
    }

    [Fact]
    public async Task GetLearningPathAsync_RequiresKey()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetLearningPathAsync(1, " ", User(UserRolesEnum.Invitado)));

        await _service.GetLearningPathAsync(1, "cpp", User(UserRolesEnum.Invitado));
        _repository.Verify(item => item.GetLearningPathAsync(1, "cpp"), Times.Once);
    }

    public static IEnumerable<object[]> InvalidLearningPaths => new[]
    {
        new object[] { new LearningPathAdminUpsertRequest { Key = " ", Title = "T", Version = 1 }, "Learning path key es requerido." },
        new object[] { new LearningPathAdminUpsertRequest { Key = "cpp", Title = "", Version = 1 }, "Learning path title es requerido." },
        new object[] { new LearningPathAdminUpsertRequest { Key = "cpp", Title = "T", Version = 0 }, "Version debe ser positiva." },
    };

    [Theory]
    [MemberData(nameof(InvalidLearningPaths))]
    public async Task CreateLearningPathAsync_ValidatesRequest(LearningPathAdminUpsertRequest request, string expectedMessage)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateLearningPathAsync(1, Admin(), request));

        Assert.Equal(expectedMessage, error.Message);
    }

    [Fact]
    public async Task CreateLearningPathAsync_RejectsNullRequest()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.CreateLearningPathAsync(1, Admin(), null!));
    }

    [Fact]
    public async Task UpdateLearningPathAsync_TrimsKeyAndValidates()
    {
        var request = new LearningPathAdminUpsertRequest { Key = "cpp", Title = "C++", Version = 2 };
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateLearningPathAsync(1, " ", Admin(), request));

        await _service.UpdateLearningPathAsync(1, " cpp ", Admin(), request);
        _repository.Verify(item => item.UpdateLearningPathAsync(1, "cpp", request), Times.Once);
    }

    [Fact]
    public async Task DeleteLearningPathAsync_TrimsKeyAndValidates()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.DeleteLearningPathAsync(1, "", Admin()));

        await _service.DeleteLearningPathAsync(1, " cpp ", Admin());
        _repository.Verify(item => item.DeleteLearningPathAsync(1, "cpp"), Times.Once);
    }

    public static IEnumerable<object[]> InvalidStages => new[]
    {
        new object[] { new LearningPathStageAdminRequest { Key = "", Name = "N", Order = 1 }, "Stage key es requerido." },
        new object[] { new LearningPathStageAdminRequest { Key = "k", Name = " ", Order = 1 }, "Stage name es requerido." },
        new object[] { new LearningPathStageAdminRequest { Key = "k", Name = "N", Order = 0 }, "Stage order debe ser positivo." },
    };

    [Theory]
    [MemberData(nameof(InvalidStages))]
    public async Task CreateAndUpdateLearningPathStage_ValidateRequest(LearningPathStageAdminRequest request, string expectedMessage)
    {
        var create = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateLearningPathStageAsync(1, "cpp", Admin(), request));
        var update = await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateLearningPathStageAsync(1, "cpp", 4, Admin(), request));

        Assert.Equal(expectedMessage, create.Message);
        Assert.Equal(expectedMessage, update.Message);
    }

    [Fact]
    public async Task LearningPathStageOperations_TrimKeyAndDelegate()
    {
        var request = new LearningPathStageAdminRequest { Key = "basics", Name = "Básico", Order = 1 };

        await _service.CreateLearningPathStageAsync(1, " cpp ", Admin(), request);
        await _service.LinkLearningPathStageAsync(1, " cpp ", 4, Admin());
        await _service.UnlinkLearningPathStageAsync(1, " cpp ", 4, Admin());
        await _service.UpdateLearningPathStageAsync(1, " cpp ", 4, Admin(), request);
        await _service.DeleteLearningPathStageAsync(1, " cpp ", 4, Admin());

        _repository.Verify(item => item.CreateLearningPathStageAsync(1, "cpp", request), Times.Once);
        _repository.Verify(item => item.LinkLearningPathStageAsync(1, "cpp", 4), Times.Once);
        _repository.Verify(item => item.UnlinkLearningPathStageAsync(1, "cpp", 4), Times.Once);
        _repository.Verify(item => item.UpdateLearningPathStageAsync(1, "cpp", 4, request), Times.Once);
        _repository.Verify(item => item.DeleteLearningPathStageAsync(1, "cpp", 4), Times.Once);
    }

    [Fact]
    public async Task LearningPathStageOperations_RejectInvalidIdsAndKeys()
    {
        var request = new LearningPathStageAdminRequest { Key = "basics", Name = "Básico", Order = 1 };

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateLearningPathStageAsync(1, " ", Admin(), request));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.LinkLearningPathStageAsync(1, " ", 4, Admin()));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.LinkLearningPathStageAsync(1, "cpp", 0, Admin()));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UnlinkLearningPathStageAsync(1, "", 4, Admin()));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UnlinkLearningPathStageAsync(1, "cpp", -1, Admin()));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateLearningPathStageAsync(1, "cpp", 0, Admin(), request));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.DeleteLearningPathStageAsync(1, "cpp", 0, Admin()));
    }

    [Fact]
    public async Task LearningPathTopicOperations_TrimKeyAndDelegate()
    {
        var request = new LearningPathTopicAdminRequest { Key = "vars", Title = "Variables", ProblemIds = new List<int> { 1000 } };

        await _service.CreateLearningPathTopicAsync(1, " cpp ", 4, Admin(), request);
        await _service.UpdateLearningPathTopicAsync(1, " cpp ", 4, 9, Admin(), request);
        await _service.DeleteLearningPathTopicAsync(1, " cpp ", 4, 9, Admin());

        _repository.Verify(item => item.CreateLearningPathTopicAsync(1, "cpp", 4, request), Times.Once);
        _repository.Verify(item => item.UpdateLearningPathTopicAsync(1, "cpp", 4, 9, request), Times.Once);
        _repository.Verify(item => item.DeleteLearningPathTopicAsync(1, "cpp", 4, 9), Times.Once);
    }

    [Fact]
    public async Task LearningPathTopicOperations_RejectInvalidInput()
    {
        var valid = new LearningPathTopicAdminRequest { Key = "vars", Title = "Variables" };

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateLearningPathTopicAsync(1, "cpp", 0, Admin(), valid));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateLearningPathTopicAsync(1, "cpp", 4, Admin(), new LearningPathTopicAdminRequest { Key = "", Title = "T" }));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateLearningPathTopicAsync(1, "cpp", 4, Admin(), new LearningPathTopicAdminRequest { Key = "k", Title = " " }));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.CreateLearningPathTopicAsync(1, "cpp", 4, Admin(), null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateLearningPathTopicAsync(1, "cpp", 0, 9, Admin(), valid));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateLearningPathTopicAsync(1, "cpp", 4, 0, Admin(), valid));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.DeleteLearningPathTopicAsync(1, "cpp", 0, 9, Admin()));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.DeleteLearningPathTopicAsync(1, "cpp", 4, 0, Admin()));
    }

    [Theory]
    [InlineData(UserRolesEnum.Docente)]
    [InlineData(UserRolesEnum.Auxiliar)]
    [InlineData(UserRolesEnum.Invitado)]
    public async Task LearningPathAdministration_IsOnlyForAdministrators(UserRolesEnum role)
    {
        var user = User(role);
        var path = new LearningPathAdminUpsertRequest { Key = "cpp", Title = "C++" };
        var stage = new LearningPathStageAdminRequest { Key = "b", Name = "B", Order = 1 };
        var topic = new LearningPathTopicAdminRequest { Key = "t", Title = "T" };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.UpdateLearningPathAsync(1, "cpp", user, path));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.DeleteLearningPathAsync(1, "cpp", user));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateLearningPathStageAsync(1, "cpp", user, stage));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.LinkLearningPathStageAsync(1, "cpp", 1, user));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.UnlinkLearningPathStageAsync(1, "cpp", 1, user));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.UpdateLearningPathStageAsync(1, "cpp", 1, user, stage));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.DeleteLearningPathStageAsync(1, "cpp", 1, user));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateLearningPathTopicAsync(1, "cpp", 1, user, topic));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.UpdateLearningPathTopicAsync(1, "cpp", 1, 1, user, topic));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.DeleteLearningPathTopicAsync(1, "cpp", 1, 1, user));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LearningPathAdministration_RejectsCrossSiteAdministrator()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.DeleteLearningPathAsync(2, "cpp", Admin()));
    }

    [Fact]
    public async Task GetLearningPathProgressAsync_RequiresKeyAndUsesCurrentUser()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetLearningPathProgressAsync(1, " ", User(UserRolesEnum.Invitado)));

        await _service.GetLearningPathProgressAsync(1, "cpp", User(UserRolesEnum.Invitado, "ana"));
        _repository.Verify(item => item.GetLearningPathProgressAsync(1, "cpp", "ana"), Times.Once);
    }

    [Fact]
    public async Task SaveLearningPathProgressAsync_RequiresKeyAndPassesRequest()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveLearningPathProgressAsync(1, "", User(UserRolesEnum.Invitado), new LearningPathProgressUpdateRequest()));

        var request = new LearningPathProgressUpdateRequest();
        await _service.SaveLearningPathProgressAsync(1, "cpp", User(UserRolesEnum.Invitado, "ana"), request);
        _repository.Verify(item => item.SaveLearningPathProgressAsync(1, "cpp", "ana", request), Times.Once);
    }

    [Theory]
    [InlineData(0, "int main(){}", "ProblemId es requerido.")]
    [InlineData(-5, "int main(){}", "ProblemId es requerido.")]
    [InlineData(1000, " ", "SourceCode es requerido.")]
    public async Task SubmitAsync_ValidatesRequest(int problemId, string source, string expectedMessage)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => _service.SubmitAsync(User(UserRolesEnum.Invitado), new AcademicSubmissionRequest { ProblemId = problemId, SourceCode = source }));

        Assert.Equal(expectedMessage, error.Message);
    }

    private void SetupCourse(AcademicCourseDetail course, bool isAdmin = false, string userId = "student")
    {
        _repository.Setup(item => item.GetCourseAsync(1, 10, userId, isAdmin)).ReturnsAsync(course);
    }

    private static AcademicCourseDetail CourseWithContent(params long[] itemIds) => new()
    {
        CanManage = true,
        Content = itemIds.Select(id => new AcademicCourseContentItem { ItemId = id }).ToList()
    };

    private static AcademicCourseAssignmentCreationRequest Assignment(string title = "Tarea", List<int>? problemIds = null) => new()
    {
        Title = title,
        ProblemIds = problemIds ?? new List<int> { 1000 }
    };

    private static CurrentUser Admin() => User(UserRolesEnum.Administrador, "admin");

    private static CurrentUser User(UserRolesEnum role, string userId = "student") => new()
    {
        UserId = userId,
        SiteId = 1,
        Role = role
    };
}
