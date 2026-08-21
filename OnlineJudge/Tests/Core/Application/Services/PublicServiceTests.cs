using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

public class PublicServiceTests
{
    [Fact]
    public async Task SubmitAsync_AllowsContestNumZeroWithoutProblemId()
    {
        PublicSubmissionRequest? capturedRequest = null;
        var publicRepository = new Mock<IPublicRepository>();
        publicRepository
            .Setup(item => item.SubmitAsync(It.IsAny<CurrentUser>(), It.IsAny<PublicSubmissionRequest>(), 2))
            .Callback<CurrentUser, PublicSubmissionRequest, int>((_, request, _) => capturedRequest = request)
            .ReturnsAsync(new PublicSubmissionResponse
            {
                SolutionId = 123,
                LanguageId = 2,
                CreatedAtUtc = DateTime.UtcNow
            });

        var service = CreateService(publicRepository.Object);

        var response = await service.SubmitAsync(new CurrentUser
        {
            UserId = "student1",
            SiteId = 1,
            Role = UserRolesEnum.Invitado
        }, new PublicSubmissionRequest
        {
            ContestId = 3040,
            Num = 0,
            SourceCode = "print(42)",
            LanguageId = 2
        });

        Assert.Equal(123, response.SolutionId);
        Assert.NotNull(capturedRequest);
        Assert.Equal(3040, capturedRequest!.ContestId);
        Assert.Equal(0, capturedRequest.Num);
        Assert.Null(capturedRequest.ContestProblemId);
        Assert.Null(capturedRequest.ProblemId);
    }

    [Fact]
    public async Task GetProblemsAsync_ClampsPagingAndRejectsInvalidContestId()
    {
        var repository = new Mock<IPublicRepository>();
        repository
            .Setup(item => item.GetProblemsAsync(1, 1, 100, "sum", null, null, null, null, null, 10, null))
            .ReturnsAsync(new PublicProblemsResponse { SiteId = 1, Page = 1, PageSize = 100 });
        var service = CreateService(repository.Object);

        var result = await service.GetProblemsAsync(1, -5, 500, "sum", null, null, null, null, null, 10);

        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.PageSize);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.GetProblemsAsync(1, 1, 10, null, null, null, null, null, null, 0));
        Assert.Equal("ContestId inválido.", error.Message);
    }

    [Fact]
    public async Task SubmitAsync_RejectsMissingProblemAndContestScope()
    {
        var service = CreateService();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitAsync(ValidUser(), new PublicSubmissionRequest
        {
            SourceCode = "print(42)",
            LanguageId = 2
        }));

        Assert.Equal("ProblemId, ContestProblemId o Num es requerido.", error.Message);
    }

    [Fact]
    public async Task SubmitAsync_RejectsShortSourceCode()
    {
        var service = CreateService();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitAsync(ValidUser(), new PublicSubmissionRequest
        {
            ProblemId = 1000,
            SourceCode = "x",
            LanguageId = 2
        }));

        Assert.Equal("El código fuente es demasiado corto.", error.Message);
    }

    [Fact]
    public async Task SubmitAsync_RejectsMissingLanguage()
    {
        var service = CreateService();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitAsync(ValidUser(), new PublicSubmissionRequest
        {
            ProblemId = 1000,
            SourceCode = "print(42)"
        }));

        Assert.Equal("LanguageId es requerido.", error.Message);
    }

    [Fact]
    public async Task SubmitAsync_DelegatesAcademicSubmissionWhenCourseOrAssignmentIsPresent()
    {
        AcademicSubmissionRequest? capturedRequest = null;
        var academicService = new Mock<IAcademicService>();
        academicService
            .Setup(item => item.SubmitAsync(It.IsAny<CurrentUser>(), It.IsAny<AcademicSubmissionRequest>()))
            .Callback<CurrentUser, AcademicSubmissionRequest>((_, request) => capturedRequest = request)
            .ReturnsAsync(new AcademicSubmissionResponse
            {
                SolutionId = 456,
                LanguageId = 2,
                CreatedAtUtc = new DateTime(2024, 1, 1)
            });
        var publicRepository = new Mock<IPublicRepository>(MockBehavior.Strict);
        var service = CreateService(publicRepository.Object, academicService: academicService.Object);

        var response = await service.SubmitAsync(ValidUser(), new PublicSubmissionRequest
        {
            ProblemId = 1000,
            CourseId = 5,
            AssignmentId = 6,
            ContestId = 3040,
            SourceCode = "print(42)",
            LanguageId = 2,
            FileName = "main.py"
        });

        Assert.Equal(456, response.SolutionId);
        Assert.Equal(2, response.LanguageId);
        Assert.False(response.AutoDetected);
        Assert.NotNull(capturedRequest);
        Assert.Equal(1000, capturedRequest!.ProblemId);
        Assert.Equal(5, capturedRequest.CourseId);
        Assert.Equal(6, capturedRequest.AssignmentId);
        Assert.Equal(3040, capturedRequest.ContestId);
        Assert.Equal("main.py", capturedRequest.FileName);
    }

    [Fact]
    public async Task GetSubmissionStatusAsync_AllowsOwnerAndMapsSolutionStatus()
    {
        var solutionService = new Mock<ISolutionService>();
        solutionService.Setup(item => item.GetSolutionByIdAsync(123)).ReturnsAsync(new Solution
        {
            SolutionId = 123,
            SiteId = 1,
            ProblemId = 1000,
            UserId = "student1",
            ContestId = 3040,
            Num = 0,
            Language = 2,
            Result = JudgeResultCodes.Accepted,
            Time = 12,
            Memory = 2048,
            PassRate = 1,
            InDate = new DateTime(2024, 1, 1),
            User = new User { UserId = "student1", UserProfile = new UserProfile { Nick = "Student" } },
            SourceCode = new SourceCode { Source = "print(42)" },
            Compileinfo = new Compileinfo { Error = "" },
            Runtimeinfo = new Runtimeinfo { Error = "" }
        });
        var academicRepository = new Mock<IAcademicRepository>(MockBehavior.Strict);
        var service = CreateService(academicRepository: academicRepository.Object, solutionService: solutionService.Object);

        var status = await service.GetSubmissionStatusAsync(ValidUser(), 123);

        Assert.Equal(123, status.SolutionId);
        Assert.Equal("A", status.ContestProblemId);
        Assert.Equal("accepted", status.StatusKey);
        Assert.Equal("Accepted", status.StatusLabel);
        Assert.Equal("finished", status.GeneralStatusKey);
        Assert.True(status.IsFinal);
        Assert.Equal("Student", status.Nick);
        Assert.Equal("print(42)", status.SourceCode);
    }

    [Fact]
    public async Task GetSubmissionStatusAsync_AllowsAcademicManagerWhenRepositoryAllowsSourceView()
    {
        var solutionService = new Mock<ISolutionService>();
        solutionService.Setup(item => item.GetSolutionByIdAsync(123)).ReturnsAsync(new Solution
        {
            SolutionId = 123,
            SiteId = 1,
            ProblemId = 1000,
            UserId = "student1",
            Language = 2,
            Result = JudgeResultCodes.Pending,
            InDate = DateTime.UtcNow
        });
        var academicRepository = new Mock<IAcademicRepository>();
        academicRepository
            .Setup(item => item.CanViewCourseSubmissionSourceAsync(1, 123, "teacher", true))
            .ReturnsAsync(true);
        var service = CreateService(academicRepository: academicRepository.Object, solutionService: solutionService.Object);

        var status = await service.GetSubmissionStatusAsync(new CurrentUser { UserId = "teacher", SiteId = 1, Role = UserRolesEnum.Docente }, 123);

        Assert.Equal(123, status.SolutionId);
    }

    [Fact]
    public async Task GetSubmissionStatusAsync_HidesOtherUsersWhenNotAllowed()
    {
        var solutionService = new Mock<ISolutionService>();
        solutionService.Setup(item => item.GetSolutionByIdAsync(123)).ReturnsAsync(new Solution
        {
            SolutionId = 123,
            SiteId = 1,
            ProblemId = 1000,
            UserId = "other",
            Language = 2,
            InDate = DateTime.UtcNow
        });
        var academicRepository = new Mock<IAcademicRepository>();
        academicRepository.Setup(item => item.CanViewCourseSubmissionSourceAsync(1, 123, "student1", false)).ReturnsAsync(false);
        var service = CreateService(academicRepository: academicRepository.Object, solutionService: solutionService.Object);

        var error = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetSubmissionStatusAsync(ValidUser(), 123));

        Assert.Equal("No se encontró el envío solicitado.", error.Message);
    }

    [Fact]
    public async Task LoginAsync_TrimsUserOrEmailAndDelegates()
    {
        var repository = new Mock<IPublicRepository>();
        repository.Setup(item => item.LoginAsync("student", "secret", 1)).ReturnsAsync(new PublicAuthenticatedUser { UserId = "student" });
        var service = CreateService(repository.Object);

        var user = await service.LoginAsync(" student ", "secret", 1);

        Assert.Equal("student", user.UserId);
        repository.Verify(item => item.LoginAsync("student", "secret", 1), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_ValidatesInput_StoresHashedPassword_AndSwallowsWelcomeEmailFailure()
    {
        string? capturedPasswordHash = null;
        var repository = new Mock<IPublicRepository>();
        repository
            .Setup(item => item.RegisterAsync("student_1", It.IsAny<string>(), "s@example.com", "Nick", null, "School", 1, "127.0.0.1"))
            .Callback<string, string, string, string?, string?, string?, int, string>((_, passwordHash, _, _, _, _, _, _) => capturedPasswordHash = passwordHash)
            .ReturnsAsync(new PublicAuthenticatedUser { UserId = "student_1", Email = "s@example.com", Nick = "Nick", SiteId = 1 });
        var welcomeEmailService = new Mock<IWelcomeEmailService>();
        welcomeEmailService.Setup(item => item.SendWelcomeAsync("s@example.com", "student_1", "Nick")).ThrowsAsync(new InvalidOperationException("smtp down"));
        var service = CreateService(repository.Object, welcomeEmailService: welcomeEmailService.Object);

        var user = await service.RegisterAsync("student_1", "secret1", " s@example.com ", " Nick ", " ", " School ", 1, "127.0.0.1");

        Assert.Equal("student_1", user.UserId);
        Assert.False(string.IsNullOrWhiteSpace(capturedPasswordHash));
        Assert.NotEqual("secret1", capturedPasswordHash);
    }

    [Theory]
    [InlineData("ab", "secret1", "s@example.com", "UserId inválido. Debe tener 3-20 caracteres alfanuméricos o _.")]
    [InlineData("student", "123", "s@example.com", "La contraseña debe tener al menos 6 caracteres.")]
    [InlineData("student", "secret1", "bad-email", "Correo electrónico inválido.")]
    public async Task RegisterAsync_RejectsInvalidInput(string userId, string password, string email, string expectedMessage)
    {
        var service = CreateService();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterAsync(userId, password, email, null, null, null, 1, "127.0.0.1"));

        Assert.Equal(expectedMessage, error.Message);
    }

    [Fact]
    public async Task RequestPasswordRecoveryAsync_SavesHashedTokenAndSendsRecoveryCode()
    {
        string? capturedHash = null;
        DateTime capturedExpiration = default;
        string? capturedRecoveryCode = null;
        var repository = new Mock<IPublicRepository>();
        repository.Setup(item => item.GetPasswordRecoveryTargetAsync("s@example.com", 1)).ReturnsAsync(new PublicPasswordRecoveryTarget
        {
            UserId = "student",
            Email = "s@example.com",
            Nick = "Student"
        });
        repository
            .Setup(item => item.SavePasswordRecoveryTokenAsync("student", 1, It.IsAny<string>(), It.IsAny<DateTime>()))
            .Callback<string, int, string, DateTime>((_, _, hash, expiresAtUtc) =>
            {
                capturedHash = hash;
                capturedExpiration = expiresAtUtc;
            })
            .Returns(Task.CompletedTask);
        var emailService = new Mock<IPasswordRecoveryEmailService>();
        emailService
            .Setup(item => item.SendRecoveryCodeAsync("s@example.com", "student", It.IsAny<string>(), "Student"))
            .Callback<string, string, string, string?>((_, _, code, _) => capturedRecoveryCode = code)
            .Returns(Task.CompletedTask);
        var configuration = new Mock<IConfiguration>();
        configuration.SetupGet(item => item["PasswordRecovery:TokenTtlMinutes"]).Returns("2");
        var service = CreateService(repository.Object, passwordRecoveryEmailService: emailService.Object, configuration: configuration.Object);

        await service.RequestPasswordRecoveryAsync(" s@example.com ", 1);

        Assert.False(string.IsNullOrWhiteSpace(capturedHash));
        Assert.Equal(64, capturedHash!.Length);
        Assert.NotEqual(capturedRecoveryCode, capturedHash);
        Assert.True(capturedExpiration > DateTime.UtcNow.AddMinutes(4));
        Assert.Equal(16, capturedRecoveryCode!.Length);
    }

    [Fact]
    public async Task RequestPasswordRecoveryAsync_CompletesSilentlyWhenEmailIsNotRegistered()
    {
        var repository = new Mock<IPublicRepository>();
        repository.Setup(item => item.GetPasswordRecoveryTargetAsync("nobody@example.com", 1)).ReturnsAsync((PublicPasswordRecoveryTarget?)null);
        var emailService = new Mock<IPasswordRecoveryEmailService>();
        var service = CreateService(repository.Object, passwordRecoveryEmailService: emailService.Object);

        await service.RequestPasswordRecoveryAsync("nobody@example.com", 1);

        repository.Verify(item => item.SavePasswordRecoveryTokenAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        emailService.Verify(item => item.SendRecoveryCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordWithRecoveryCodeAsync_RejectsInvalidOrExpiredCode()
    {
        var repository = new Mock<IPublicRepository>();
        repository.Setup(item => item.ResetPasswordWithTokenAsync("s@example.com", 1, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        var service = CreateService(repository.Object);

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ResetPasswordWithRecoveryCodeAsync("s@example.com", "ABCDEF", 1));

        Assert.Equal("El código de recuperación es inválido o expiró.", error.Message);
    }

    private static PublicService CreateService(
        IPublicRepository? publicRepository = null,
        IAcademicRepository? academicRepository = null,
        IAcademicService? academicService = null,
        ISolutionService? solutionService = null,
        IPasswordRecoveryEmailService? passwordRecoveryEmailService = null,
        IWelcomeEmailService? welcomeEmailService = null,
        IConfiguration? configuration = null)
    {
        return new PublicService(
            publicRepository ?? Mock.Of<IPublicRepository>(),
            academicRepository ?? Mock.Of<IAcademicRepository>(),
            academicService ?? Mock.Of<IAcademicService>(),
            solutionService ?? Mock.Of<ISolutionService>(),
            passwordRecoveryEmailService ?? Mock.Of<IPasswordRecoveryEmailService>(),
            welcomeEmailService ?? Mock.Of<IWelcomeEmailService>(),
            configuration ?? Mock.Of<IConfiguration>());
    }

    private static CurrentUser ValidUser()
    {
        return new CurrentUser
        {
            UserId = "student1",
            SiteId = 1,
            Role = UserRolesEnum.Invitado
        };
    }
}
