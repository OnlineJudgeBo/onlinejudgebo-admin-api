using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

public class PublicServiceCoverageTests
{
    private readonly Mock<IPublicRepository> _repository = new();
    private readonly Mock<IAcademicRepository> _academicRepository = new();
    private readonly Mock<IAcademicService> _academicService = new();
    private readonly Mock<ISolutionService> _solutionService = new();
    private readonly Mock<IPasswordRecoveryEmailService> _recoveryEmail = new();
    private readonly Mock<IWelcomeEmailService> _welcomeEmail = new();
    private IConfiguration _configuration = new ConfigurationBuilder().Build();

    private PublicService Service => new(
        _repository.Object,
        _academicRepository.Object,
        _academicService.Object,
        _solutionService.Object,
        _recoveryEmail.Object,
        _welcomeEmail.Object,
        _configuration);

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var config = new ConfigurationBuilder().Build();
        Assert.Throws<ArgumentNullException>(() => new PublicService(null!, _academicRepository.Object, _academicService.Object, _solutionService.Object, _recoveryEmail.Object, _welcomeEmail.Object, config));
        Assert.Throws<ArgumentNullException>(() => new PublicService(_repository.Object, null!, _academicService.Object, _solutionService.Object, _recoveryEmail.Object, _welcomeEmail.Object, config));
        Assert.Throws<ArgumentNullException>(() => new PublicService(_repository.Object, _academicRepository.Object, null!, _solutionService.Object, _recoveryEmail.Object, _welcomeEmail.Object, config));
        Assert.Throws<ArgumentNullException>(() => new PublicService(_repository.Object, _academicRepository.Object, _academicService.Object, null!, _recoveryEmail.Object, _welcomeEmail.Object, config));
        Assert.Throws<ArgumentNullException>(() => new PublicService(_repository.Object, _academicRepository.Object, _academicService.Object, _solutionService.Object, null!, _welcomeEmail.Object, config));
        Assert.Throws<ArgumentNullException>(() => new PublicService(_repository.Object, _academicRepository.Object, _academicService.Object, _solutionService.Object, _recoveryEmail.Object, null!, config));
        Assert.Throws<ArgumentNullException>(() => new PublicService(_repository.Object, _academicRepository.Object, _academicService.Object, _solutionService.Object, _recoveryEmail.Object, _welcomeEmail.Object, null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SiteScopedQueries_RejectInvalidSite(int siteId)
    {
        var service = Service;

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetDashboardAsync(siteId));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetProblemsAsync(siteId, 1, 10, null, null, null, null, null, null, null));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetProblemDetailAsync(siteId, 1));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetContestProblemDetailAsync(siteId, 1, "A"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetProblemStatisticsAsync(siteId, 1));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetProblemFiltersAsync(siteId));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetRankingAsync(siteId, 10, null));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetTopicsAsync(siteId));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetContestsAsync(siteId, null, null, null, 1, 10, null));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetContestReportAsync(siteId, 1));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetSubmissionsAsync(siteId, 1, 10, null, null, null, null, null));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetRecentSubmissionsAsync(siteId, 1, 10, null, null));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetOnlineUsersAsync(siteId, 10));
        await Assert.ThrowsAsync<ArgumentException>(() => service.LoginAsync("u", "p", siteId));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterAsync("user1", "secret1", "a@b.c", null, null, null, siteId, "ip"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RequestPasswordRecoveryAsync("a@b.c", siteId));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ResetPasswordWithRecoveryCodeAsync("a@b.c", "ABCDEF", siteId));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SimpleQueries_DelegateToRepository()
    {
        var service = Service;

        await service.GetDashboardAsync(1);
        await service.GetProblemDetailAsync(1, 1000);
        await service.GetProblemStatisticsAsync(1, 1000);
        await service.GetProblemFiltersAsync(1);
        await service.GetTopicsAsync(1);
        await service.GetLanguagesAsync();

        _repository.Verify(item => item.GetDashboardAsync(1), Times.Once);
        _repository.Verify(item => item.GetProblemDetailAsync(1, 1000), Times.Once);
        _repository.Verify(item => item.GetProblemStatisticsAsync(1, 1000), Times.Once);
        _repository.Verify(item => item.GetProblemFiltersAsync(1), Times.Once);
        _repository.Verify(item => item.GetTopicsAsync(1), Times.Once);
        _repository.Verify(item => item.GetLanguagesAsync(), Times.Once);
    }

    [Fact]
    public async Task ProblemIdQueries_RejectNonPositiveIds()
    {
        var service = Service;

        Assert.Equal("ProblemId inválido.", (await Assert.ThrowsAsync<ArgumentException>(() => service.GetProblemDetailAsync(1, 0))).Message);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetProblemStatisticsAsync(1, -1));
    }

    [Fact]
    public async Task GetProblemsAsync_PassesFiltersAndClampsPaging()
    {
        var user = User();

        await Service.GetProblemsAsync(1, 0, 1000, "suma", 2024, "icpc", "boca", "dp", "name", 7, user);

        _repository.Verify(item => item.GetProblemsAsync(1, 1, 100, "suma", 2024, "icpc", "boca", "dp", "name", 7, user), Times.Once);
    }

    [Fact]
    public async Task GetProblemsAsync_ClampsLowerPageSize()
    {
        await Service.GetProblemsAsync(1, 3, 0, null, null, null, null, null, null, null);

        _repository.Verify(item => item.GetProblemsAsync(1, 3, 1, null, null, null, null, null, null, null, null), Times.Once);
    }

    [Theory]
    [InlineData(0, "A", "ContestId inválido.")]
    [InlineData(5, " ", "ContestProblemId inválido.")]
    [InlineData(5, "", "ContestProblemId inválido.")]
    public async Task GetContestProblemDetailAsync_ValidatesInput(int contestId, string contestProblemId, string expectedMessage)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service.GetContestProblemDetailAsync(1, contestId, contestProblemId));

        Assert.Equal(expectedMessage, error.Message);
    }

    [Fact]
    public async Task GetContestProblemDetailAsync_Delegates()
    {
        var user = User();

        await Service.GetContestProblemDetailAsync(1, 5, "B", user);

        _repository.Verify(item => item.GetContestProblemDetailAsync(1, 5, "B", user), Times.Once);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(50, 50)]
    [InlineData(999, 200)]
    public async Task GetRankingAsync_ClampsLimit(int limit, int expected)
    {
        await Service.GetRankingAsync(1, limit, "global");

        _repository.Verify(item => item.GetRankingAsync(1, expected, "global"), Times.Once);
    }

    [Fact]
    public async Task GetContestsAsync_ClampsPagingAndPassesFilters()
    {
        await Service.GetContestsAsync(1, "running", "easy", "start", -3, 500, "icpc");

        _repository.Verify(item => item.GetContestsAsync(1, "running", "easy", "start", 1, 100, "icpc"), Times.Once);
    }

    [Fact]
    public async Task GetContestReportAsync_ValidatesAndDelegates()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service.GetContestReportAsync(1, 0));

        var user = User();
        await Service.GetContestReportAsync(1, 5, user);
        _repository.Verify(item => item.GetContestReportAsync(1, 5, user), Times.Once);
    }

    public static IEnumerable<object?[]> InvalidUsers => new[]
    {
        new object?[] { null },
        new object?[] { new CurrentUser { UserId = "", SiteId = 1 } },
        new object?[] { new CurrentUser { UserId = " ", SiteId = 1 } },
        new object?[] { new CurrentUser { UserId = "defaultUserId", SiteId = 1 } },
        new object?[] { new CurrentUser { UserId = "ana", SiteId = 0 } },
    };

    [Theory]
    [MemberData(nameof(InvalidUsers))]
    public async Task UserScopedOperations_RejectInvalidUserContext(CurrentUser? user)
    {
        var service = Service;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CanDownloadContestReportCsvAsync(user!, 1, 5));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RegisterForContestAsync(user!, 1, 5));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetOwnSubmissionsAsync(user!, 1, 10));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetOwnSubmissionSourceCodesAsync(user!));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SubmitAsync(user!, new PublicSubmissionRequest()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetSubmissionStatusAsync(user!, 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAuthenticatedUserAsync(user!));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ContestUserOperations_ValidateAndDelegate()
    {
        var user = User();
        var service = Service;

        await Assert.ThrowsAsync<ArgumentException>(() => service.CanDownloadContestReportCsvAsync(user, 1, 0));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterForContestAsync(user, 0, 5));
        await service.CanDownloadContestReportCsvAsync(user, 1, 5);
        await service.RegisterForContestAsync(user, 1, 5);

        _repository.Verify(item => item.CanDownloadContestReportCsvAsync(user, 1, 5), Times.Once);
        _repository.Verify(item => item.RegisterForContestAsync(user, 1, 5), Times.Once);
    }

    [Fact]
    public async Task GetSubmissionsAsync_TrimsUserFilterAndClampsPaging()
    {
        await Service.GetSubmissionsAsync(1, 0, 101, 5, 1000, "  ana  ", 2, "ac");

        _repository.Verify(item => item.GetSubmissionsAsync(1, 1, 100, 5, 1000, "ana", 2, "ac", null), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetSubmissionsAsync_TreatsBlankUserFilterAsNull(string? userId)
    {
        await Service.GetSubmissionsAsync(1, 1, 10, null, null, userId, null, null);

        _repository.Verify(item => item.GetSubmissionsAsync(1, 1, 10, null, null, null, null, null, null), Times.Once);
    }

    [Fact]
    public async Task GetSubmissionsAsync_RejectsInvalidOptionalIds()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service.GetSubmissionsAsync(1, 1, 10, 0, null, null, null, null));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.GetSubmissionsAsync(1, 1, 10, null, -2, null, null, null));
    }

    [Fact]
    public async Task GetOwnSubmissionsAsync_AllowsLargerPageSize()
    {
        var user = User();

        await Service.GetOwnSubmissionsAsync(user, 0, 400);
        await Service.GetOwnSubmissionsAsync(user, 2, 9000);

        _repository.Verify(item => item.GetOwnSubmissionsAsync(user, 1, 400), Times.Once);
        _repository.Verify(item => item.GetOwnSubmissionsAsync(user, 2, 500), Times.Once);
    }

    [Fact]
    public async Task GetRecentSubmissionsAsync_ValidatesIdsAndDelegates()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service.GetRecentSubmissionsAsync(1, 1, 10, 0, null));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service.GetRecentSubmissionsAsync(1, 1, 10, null, 0));
        Assert.Equal("CourseId inválido.", error.Message);

        await Service.GetRecentSubmissionsAsync(1, -1, 1000, 5, 3);
        _repository.Verify(item => item.GetRecentSubmissionsAsync(1, 1, 100, 5, 3L, null), Times.Once);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(15, 15)]
    [InlineData(99999, 1440)]
    public async Task GetOnlineUsersAsync_ClampsWindow(int window, int expected)
    {
        await Service.GetOnlineUsersAsync(1, window);

        _repository.Verify(item => item.GetOnlineUsersAsync(1, expected), Times.Once);
    }

    [Fact]
    public async Task GetOwnSubmissionSourceCodesAsync_Delegates()
    {
        var user = User();

        await Service.GetOwnSubmissionSourceCodesAsync(user);

        _repository.Verify(item => item.GetOwnSubmissionSourceCodesAsync(user), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_RejectsTooLongSource()
    {
        var request = new PublicSubmissionRequest { ProblemId = 1000, LanguageId = 1, SourceCode = new string('a', 200001) };

        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service.SubmitAsync(User(), request));

        Assert.Equal("El código fuente es demasiado largo.", error.Message);
    }

    [Fact]
    public async Task SubmitAsync_AcceptsSourceAtMaximumLength()
    {
        var request = new PublicSubmissionRequest { ProblemId = 1000, LanguageId = 1, SourceCode = new string('a', 200000) };

        await Service.SubmitAsync(User(), request);

        _repository.Verify(item => item.SubmitAsync(It.IsAny<CurrentUser>(), request, 1), Times.Once);
    }

    [Theory]
    [InlineData("    abcd    ")]
    [InlineData("")]
    public async Task SubmitAsync_CountsTrimmedSourceLength(string source)
    {
        var request = new PublicSubmissionRequest { ProblemId = 1000, LanguageId = 1, SourceCode = source };

        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service.SubmitAsync(User(), request));

        Assert.Equal("El código fuente es demasiado corto.", error.Message);
    }

    [Fact]
    public async Task SubmitAsync_AcceptsContestProblemLetter()
    {
        var request = new PublicSubmissionRequest { ContestId = 5, ContestProblemId = "A", LanguageId = 2, SourceCode = "int main(){}" };

        await Service.SubmitAsync(User(), request);

        _repository.Verify(item => item.SubmitAsync(It.IsAny<CurrentUser>(), request, 2), Times.Once);
    }

    [Theory]
    [InlineData(null, "A", null)]
    [InlineData(null, null, 0)]
    [InlineData(5, null, -1)]
    [InlineData(5, " ", null)]
    public async Task SubmitAsync_RequiresProblemOrCompleteContestReference(int? contestId, string? contestProblemId, int? num)
    {
        var request = new PublicSubmissionRequest { ContestId = contestId, ContestProblemId = contestProblemId, Num = num, LanguageId = 1, SourceCode = "int main(){}" };

        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service.SubmitAsync(User(), request));

        Assert.Equal("ProblemId, ContestProblemId o Num es requerido.", error.Message);
    }

    [Fact]
    public async Task SubmitAsync_AcademicSubmissionRequiresProblemId()
    {
        var request = new PublicSubmissionRequest { ContestId = 5, ContestProblemId = "A", CourseId = 3, LanguageId = 1, SourceCode = "int main(){}" };

        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service.SubmitAsync(User(), request));

        Assert.Equal("ProblemId es requerido para envíos académicos.", error.Message);
        _academicService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SubmitAsync_AssignmentOnlyIsAlsoAcademic()
    {
        var createdAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        _academicService
            .Setup(item => item.SubmitAsync(It.IsAny<CurrentUser>(), It.IsAny<AcademicSubmissionRequest>()))
            .ReturnsAsync(new AcademicSubmissionResponse { SolutionId = 77, CreatedAtUtc = createdAt });
        var request = new PublicSubmissionRequest { ProblemId = 1000, AssignmentId = 9, LanguageId = 3, SourceCode = "int main(){}", FileName = "a.cpp", ClientIp = "1.2.3.4" };

        var response = await Service.SubmitAsync(User(), request);

        Assert.Equal(77, response.SolutionId);
        Assert.Equal(3, response.LanguageId);
        Assert.False(response.AutoDetected);
        Assert.Equal(createdAt, response.CreatedAtUtc);
        _academicService.Verify(item => item.SubmitAsync(It.IsAny<CurrentUser>(), It.Is<AcademicSubmissionRequest>(r =>
            r.ProblemId == 1000 && r.AssignmentId == 9 && r.CourseId == null && r.LanguageId == 3 && r.FileName == "a.cpp" && r.ClientIp == "1.2.3.4")), Times.Once);
        _repository.Verify(item => item.SubmitAsync(It.IsAny<CurrentUser>(), It.IsAny<PublicSubmissionRequest>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetSubmissionStatusAsync_RejectsInvalidId()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service.GetSubmissionStatusAsync(User(), 0));
    }

    [Fact]
    public async Task GetSubmissionStatusAsync_HidesMissingSolution()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.GetSubmissionStatusAsync(User(), 10));
    }

    [Fact]
    public async Task GetSubmissionStatusAsync_HidesSolutionFromAnotherSiteEvenForOwner()
    {
        _solutionService.Setup(item => item.GetSolutionByIdAsync(10)).ReturnsAsync(new Solution { SolutionId = 10, SiteId = 2, UserId = "student1" });

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.GetSubmissionStatusAsync(User(), 10));
        _academicRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetSubmissionStatusAsync_OwnerMatchIsCaseInsensitiveAndSkipsCourseCheck()
    {
        _solutionService.Setup(item => item.GetSolutionByIdAsync(10)).ReturnsAsync(new Solution { SolutionId = 10, SiteId = 1, UserId = "STUDENT1" });

        await Service.GetSubmissionStatusAsync(User(), 10);

        _academicRepository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(UserRolesEnum.Administrador, true)]
    [InlineData(UserRolesEnum.Docente, true)]
    [InlineData(UserRolesEnum.Auxiliar, true)]
    [InlineData(UserRolesEnum.Invitado, false)]
    public async Task GetSubmissionStatusAsync_PassesManagerFlagToCourseCheck(UserRolesEnum role, bool isManager)
    {
        _solutionService.Setup(item => item.GetSolutionByIdAsync(10)).ReturnsAsync(new Solution { SolutionId = 10, SiteId = 1, UserId = "other" });
        _academicRepository.Setup(item => item.CanViewCourseSubmissionSourceAsync(1, 10, "student1", isManager)).ReturnsAsync(true);

        await Service.GetSubmissionStatusAsync(User(role), 10);

        _academicRepository.Verify(item => item.CanViewCourseSubmissionSourceAsync(1, 10, "student1", isManager), Times.Once);
    }

    [Theory]
    [InlineData("", "pw")]
    [InlineData(" ", "pw")]
    [InlineData("user", "")]
    [InlineData("user", " ")]
    public async Task LoginAsync_RejectsMissingCredentials(string user, string password)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service.LoginAsync(user, password, 1));

        Assert.Equal("Credenciales inválidas.", error.Message);
    }

    [Fact]
    public async Task RegisterAsync_TrimsOptionalFieldsAndSendsWelcomeWithSite()
    {
        _repository
            .Setup(item => item.RegisterAsync("new_user", It.IsAny<string>(), "a@b.c", null, "Pérez", null, 2, "1.1.1.1"))
            .ReturnsAsync(new PublicAuthenticatedUser { UserId = "new_user", Email = "a@b.c", Nick = "", SiteId = 2 });

        var user = await Service.RegisterAsync("new_user", "secret1", " a@b.c ", "  ", " Pérez ", "", 2, "1.1.1.1");

        Assert.Equal("new_user", user.UserId);
        _welcomeEmail.Verify(item => item.SendWelcomeAsync("a@b.c", "new_user", 2, ""), Times.Once);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("user_name_20_chars__")]
    [InlineData("A1_b2")]
    public async Task RegisterAsync_AcceptsValidUserIds(string userId)
    {
        _repository
            .Setup(item => item.RegisterAsync(userId, It.IsAny<string>(), "a@b.c", null, null, null, 1, "ip"))
            .ReturnsAsync(new PublicAuthenticatedUser { UserId = userId, Email = "a@b.c" });

        var user = await Service.RegisterAsync(userId, "secret1", "a@b.c", null, null, null, 1, "ip");

        Assert.Equal(userId, user.UserId);
    }

    [Fact]
    public async Task RegisterAsync_StoresVerifiableLegacyHash()
    {
        string? storedHash = null;
        _repository
            .Setup(item => item.RegisterAsync("new_user", It.IsAny<string>(), "a@b.c", null, null, null, 1, "ip"))
            .Callback<string, string, string, string?, string?, string?, int, string>((_, hash, _, _, _, _, _, _) => storedHash = hash)
            .ReturnsAsync(new PublicAuthenticatedUser { UserId = "new_user", Email = "a@b.c" });

        await Service.RegisterAsync("new_user", "secret1", "a@b.c", null, null, null, 1, "ip");

        Assert.True(VerifyLegacyHash("secret1", storedHash!));
        Assert.False(VerifyLegacyHash("secret2", storedHash!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RequestPasswordRecoveryAsync_RequiresEmail(string email)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service.RequestPasswordRecoveryAsync(email, 1));

        Assert.Equal("Correo electrónico requerido.", error.Message);
    }

    [Fact]
    public async Task RequestPasswordRecoveryAsync_RejectsMalformedEmail()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service.RequestPasswordRecoveryAsync("no-at-sign", 1));

        Assert.Equal("Correo electrónico inválido.", error.Message);
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null, 30)]
    [InlineData("1", 5)]
    [InlineData("45", 45)]
    [InlineData("abc", 30)]
    public async Task RequestPasswordRecoveryAsync_UsesConfiguredTtlWithFiveMinuteMinimum(string? ttl, int expectedMinutes)
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Email:PasswordRecovery:TokenTtlMinutes"] = ttl })
            .Build();
        DateTime expiresAt = default;
        _repository.Setup(item => item.GetPasswordRecoveryTargetAsync("a@b.c", 1)).ReturnsAsync(new PublicPasswordRecoveryTarget { UserId = "ana", Email = "a@b.c", Nick = "Ana" });
        _repository
            .Setup(item => item.SavePasswordRecoveryTokenAsync("ana", 1, It.IsAny<string>(), It.IsAny<DateTime>()))
            .Callback<string, int, string, DateTime>((_, _, _, expires) => expiresAt = expires)
            .Returns(Task.CompletedTask);

        var before = DateTime.UtcNow;
        await Service.RequestPasswordRecoveryAsync(" a@b.c ", 1);

        Assert.InRange(expiresAt, before.AddMinutes(expectedMinutes).AddSeconds(-1), DateTime.UtcNow.AddMinutes(expectedMinutes).AddSeconds(1));
    }

    [Fact]
    public async Task RequestPasswordRecoveryAsync_StoresSha256OfTheCodeThatIsEmailed()
    {
        string? storedHash = null;
        string? emailedCode = null;
        _repository.Setup(item => item.GetPasswordRecoveryTargetAsync("a@b.c", 3)).ReturnsAsync(new PublicPasswordRecoveryTarget { UserId = "ana", Email = "a@b.c", Nick = "Ana" });
        _repository
            .Setup(item => item.SavePasswordRecoveryTokenAsync("ana", 3, It.IsAny<string>(), It.IsAny<DateTime>()))
            .Callback<string, int, string, DateTime>((_, _, hash, _) => storedHash = hash)
            .Returns(Task.CompletedTask);
        _recoveryEmail
            .Setup(item => item.SendRecoveryCodeAsync("a@b.c", "ana", It.IsAny<string>(), 3, "Ana"))
            .Callback<string, string, string, int, string?>((_, _, code, _, _) => emailedCode = code)
            .Returns(Task.CompletedTask);

        await Service.RequestPasswordRecoveryAsync("a@b.c", 3);

        Assert.Matches("^[0-9A-F]{16}$", emailedCode!);
        Assert.Equal(Sha256Hex(emailedCode!), storedHash);
    }

    [Theory]
    [InlineData("", "ABCDEF", "Correo y código de recuperación son requeridos.")]
    [InlineData("a@b.c", " ", "Correo y código de recuperación son requeridos.")]
    [InlineData("invalid", "ABCDEF", "Correo electrónico inválido.")]
    [InlineData("a@b.c", " ABC12 ", "Código de recuperación inválido.")]
    public async Task ResetPasswordWithRecoveryCodeAsync_ValidatesInput(string email, string code, string expectedMessage)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service.ResetPasswordWithRecoveryCodeAsync(email, code, 1));

        Assert.Equal(expectedMessage, error.Message);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResetPasswordWithRecoveryCodeAsync_NormalizesCodeAndSetsItAsPassword()
    {
        string? tokenHash = null;
        string? passwordHash = null;
        _repository
            .Setup(item => item.ResetPasswordWithTokenAsync("a@b.c", 1, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()))
            .Callback<string, int, string, DateTime, string>((_, _, hash, _, newPassword) => { tokenHash = hash; passwordHash = newPassword; })
            .ReturnsAsync(true);

        await Service.ResetPasswordWithRecoveryCodeAsync(" a@b.c ", " abcdef12 ", 1);

        Assert.Equal(Sha256Hex("ABCDEF12"), tokenHash);
        Assert.True(VerifyLegacyHash("ABCDEF12", passwordHash!));
    }

    [Fact]
    public async Task GetAuthenticatedUserAsync_UsesUserAndSite()
    {
        await Service.GetAuthenticatedUserAsync(new CurrentUser { UserId = "ana", SiteId = 4 });

        _repository.Verify(item => item.GetAuthenticatedUserAsync("ana", 4), Times.Once);
    }

    private static CurrentUser User(UserRolesEnum role = UserRolesEnum.Invitado) => new()
    {
        UserId = "student1",
        SiteId = 1,
        Role = role
    };

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    // Legacy HUSTOJ format: base64(sha1(md5hex(password) + salt) + salt), salt = 4 hex chars.
    private static bool VerifyLegacyHash(string password, string stored)
    {
        var bytes = Convert.FromBase64String(stored);
        var salt = Encoding.UTF8.GetString(bytes, 20, bytes.Length - 20);
        var md5 = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();
        return SHA1.HashData(Encoding.UTF8.GetBytes(md5 + salt)).SequenceEqual(bytes.Take(20));
    }
}
