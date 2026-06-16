using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Core.Application.Services.Implementations.IdeIntegration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;

public class IdeSubmissionServiceTests
{
    [Fact]
    public async Task SubmitAsync_PreservesContestClaimsForContestProblemA()
    {
        var validator = CreateValidator(new IdeLaunchClaims(
            "student1",
            SiteId: 1,
            ProblemId: 1000,
            ContestId: 3040,
            Num: 0,
            AllowedLanguages: new[] { 2 }));

        PublicSubmissionRequest? capturedRequest = null;
        CurrentUser? capturedUser = null;
        var publicService = new Mock<IPublicService>();
        publicService
            .Setup(item => item.SubmitAsync(It.IsAny<CurrentUser>(), It.IsAny<PublicSubmissionRequest>()))
            .Callback<CurrentUser, PublicSubmissionRequest>((user, request) =>
            {
                capturedUser = user;
                capturedRequest = request;
            })
            .ReturnsAsync(new PublicSubmissionResponse
            {
                SolutionId = 123,
                LanguageId = 2,
                CreatedAtUtc = DateTime.UtcNow
            });

        var service = new IdeSubmissionService(validator.Object, publicService.Object, Mock.Of<IIdeCustomInputRepository>());

        var response = await service.SubmitAsync("launch-token", new IdeSubmissionRequest
        {
            ProblemId = "1000",
            SourceCode = "print(42)",
            LanguageId = 2
        });

        Assert.Equal("123", response.SubmissionId);
        Assert.Equal("123", response.Id);
        Assert.Equal("/api/patito-ide/submissions/123", response.StatusUrl);
        Assert.NotNull(capturedUser);
        Assert.Equal("student1", capturedUser!.UserId);
        Assert.Equal(1, capturedUser.SiteId);
        Assert.Equal(UserRolesEnum.Invitado, capturedUser.Role);
        Assert.NotNull(capturedRequest);
        Assert.Equal(1000, capturedRequest!.ProblemId);
        Assert.Equal(3040, capturedRequest.ContestId);
        Assert.Equal(0, capturedRequest.Num);
        Assert.Equal("A", capturedRequest.ContestProblemId);
        Assert.Equal(2, capturedRequest.LanguageId);
    }

    [Fact]
    public async Task SubmitAsync_UsesPayloadContestMetadataWhenTokenDoesNotCarryContestClaims()
    {
        var validator = CreateValidator(new IdeLaunchClaims(
            "student1",
            SiteId: 1,
            ProblemId: 1000,
            ContestId: null,
            Num: null,
            AllowedLanguages: new[] { 2 }));

        PublicSubmissionRequest? capturedRequest = null;
        var publicService = new Mock<IPublicService>();
        publicService
            .Setup(item => item.SubmitAsync(It.IsAny<CurrentUser>(), It.IsAny<PublicSubmissionRequest>()))
            .Callback<CurrentUser, PublicSubmissionRequest>((_, request) => capturedRequest = request)
            .ReturnsAsync(new PublicSubmissionResponse
            {
                SolutionId = 456,
                LanguageId = 2,
                CreatedAtUtc = DateTime.UtcNow
            });

        var service = new IdeSubmissionService(validator.Object, publicService.Object, Mock.Of<IIdeCustomInputRepository>());

        var response = await service.SubmitAsync("launch-token", new IdeSubmissionRequest
        {
            ProblemId = "1000",
            ContestId = 3040,
            Num = 0,
            SourceCode = "print(42)",
            LanguageId = 2
        });

        Assert.Equal("456", response.SubmissionId);
        Assert.NotNull(capturedRequest);
        Assert.Equal(3040, capturedRequest!.ContestId);
        Assert.Equal(0, capturedRequest.Num);
        Assert.Equal("A", capturedRequest.ContestProblemId);
    }

    [Fact]
    public async Task SubmitAsync_RejectsContestMismatchBetweenTokenAndPayload()
    {
        var validator = CreateValidator(new IdeLaunchClaims(
            "student1",
            SiteId: 1,
            ProblemId: 1000,
            ContestId: 3040,
            Num: 0,
            AllowedLanguages: new[] { 2 }));

        var publicService = new Mock<IPublicService>(MockBehavior.Strict);
        var service = new IdeSubmissionService(validator.Object, publicService.Object, Mock.Of<IIdeCustomInputRepository>());

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SubmitAsync("launch-token", new IdeSubmissionRequest
        {
            ProblemId = "1000",
            ContestId = 9999,
            Num = 0,
            SourceCode = "print(42)",
            LanguageId = 2
        }));

        Assert.Equal("El token de IDE no permite enviar a este concurso.", error.Message);
    }

    [Fact]
    public async Task SubmitAsync_RejectsProblemOutsideLaunchToken()
    {
        var validator = CreateValidator(new IdeLaunchClaims(
            "student1",
            SiteId: 1,
            ProblemId: 1000,
            ContestId: null,
            Num: null,
            AllowedLanguages: Array.Empty<int>()));

        var publicService = new Mock<IPublicService>(MockBehavior.Strict);
        var service = new IdeSubmissionService(validator.Object, publicService.Object, Mock.Of<IIdeCustomInputRepository>());

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SubmitAsync("launch-token", new IdeSubmissionRequest
        {
            ProblemId = "2000",
            SourceCode = "print(42)",
            LanguageId = 2
        }));

        Assert.Equal("El token de IDE no permite enviar a este problema.", error.Message);
    }

    [Fact]
    public async Task SubmitAsync_RejectsLanguageOutsideLaunchToken()
    {
        var validator = CreateValidator(new IdeLaunchClaims(
            "student1",
            SiteId: 1,
            ProblemId: 1000,
            ContestId: null,
            Num: null,
            AllowedLanguages: new[] { 3 }));

        var publicService = new Mock<IPublicService>(MockBehavior.Strict);
        var service = new IdeSubmissionService(validator.Object, publicService.Object, Mock.Of<IIdeCustomInputRepository>());

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SubmitAsync("launch-token", new IdeSubmissionRequest
        {
            ProblemId = "1000",
            SourceCode = "print(42)",
            LanguageId = 2
        }));

        Assert.Equal("El token de IDE no permite usar este lenguaje.", error.Message);
    }

    [Fact]
    public async Task SubmitAsync_ConvertsInvalidTokenToUnauthorizedError()
    {
        var validator = new Mock<IIdeLaunchTokenValidator>();
        validator
            .Setup(item => item.Validate("bad-token"))
            .Throws(new ArgumentException("bad token"));

        var publicService = new Mock<IPublicService>(MockBehavior.Strict);
        var service = new IdeSubmissionService(validator.Object, publicService.Object, Mock.Of<IIdeCustomInputRepository>());

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SubmitAsync("bad-token", new IdeSubmissionRequest
        {
            ProblemId = "1000",
            SourceCode = "print(42)",
            LanguageId = 2
        }));

        Assert.Equal("Token de IDE inválido o expirado.", error.Message);
        Assert.IsType<ArgumentException>(error.InnerException);
    }

    [Fact]
    public async Task CustomInputAsync_PersistsRunWithTokenScopeAndReturnsPatitoIdeStatusUrl()
    {
        var validator = CreateValidator(new IdeLaunchClaims(
            "student1",
            SiteId: 1,
            ProblemId: 1000,
            ContestId: 3040,
            Num: 1,
            AllowedLanguages: new[] { 2 }));

        IdeCustomInputRunCreation? capturedRun = null;
        var customInputRepository = new Mock<IIdeCustomInputRepository>();
        customInputRepository
            .Setup(item => item.CreateCustomInputRunAsync(It.IsAny<IdeCustomInputRunCreation>()))
            .Callback<IdeCustomInputRunCreation>(run => capturedRun = run)
            .ReturnsAsync(789);

        var service = new IdeSubmissionService(validator.Object, Mock.Of<IPublicService>(), customInputRepository.Object);

        var response = await service.CustomInputAsync("launch-token", new IdeSubmissionRequest
        {
            ProblemId = "1000",
            ContestId = 3040,
            Num = 1,
            SourceCode = "print(input())",
            LanguageId = 2,
            Stdin = "42",
            Testcases = new[] { new IdeTestcaseRequest { Input = "42", ExpectedOutput = "42" } }
        });

        Assert.Equal("789", response.RunId);
        Assert.Equal("789", response.Id);
        Assert.Equal("/api/patito-ide/runs/789", response.StatusUrl);
        Assert.NotNull(capturedRun);
        Assert.Equal("student1", capturedRun!.UserId);
        Assert.Equal(1, capturedRun.SiteId);
        Assert.Equal(1000, capturedRun.ProblemId);
        Assert.Equal(2, capturedRun.LanguageId);
        Assert.Equal(3040, capturedRun.ContestId);
        Assert.Equal(1, capturedRun.Num);
        Assert.Equal("print(input())", capturedRun.SourceCode);
        Assert.Equal("42", capturedRun.Stdin);
        Assert.Single(capturedRun.Testcases);
    }

    [Fact]
    public async Task GetStatusAsync_MapsCustomInputDoneRuntimeMessageToStdout()
    {
        var validator = CreateValidator(new IdeLaunchClaims(
            "student1",
            SiteId: 1,
            ProblemId: 1000,
            ContestId: null,
            Num: null,
            AllowedLanguages: Array.Empty<int>()));

        var publicService = new Mock<IPublicService>();
        publicService
            .Setup(item => item.GetSubmissionStatusAsync(It.IsAny<CurrentUser>(), 789))
            .ReturnsAsync(new PublicSubmissionStatusResponse
            {
                SolutionId = 789,
                ResultCode = JudgeResultCodes.TestRunDone,
                StatusKey = "test_run",
                StatusLabel = "Test Running Done",
                GeneralStatusKey = "evaluating",
                RuntimeMessage = "hello\n",
                CompileMessage = null,
                TimeMs = 8,
                MemoryKb = 1024,
                IsFinal = false
            });

        var customInputRepository = new Mock<IIdeCustomInputRepository>();
        customInputRepository.Setup(item => item.IsCustomInputAsync(789)).ReturnsAsync(true);
        var service = new IdeSubmissionService(validator.Object, publicService.Object, customInputRepository.Object);

        var response = await service.GetStatusAsync("launch-token", 789);

        Assert.Equal("789", response.SubmissionId);
        Assert.Equal("789", response.Id);
        Assert.Equal("completed", response.Phase);
        Assert.Equal("Accepted", response.Verdict);
        Assert.Equal("hello\n", response.Stdout);
        Assert.Equal(string.Empty, response.Stderr);
        Assert.Equal(string.Empty, response.CompileErrors);
        Assert.Equal(8, response.RuntimeMs);
        Assert.Equal(1024, response.MemoryKb);
        Assert.Contains("Test Running Done", response.Logs);
    }

    [Fact]
    public async Task GetStatusAsync_MapsCompileErrorToCompletedCompilationError()
    {
        var validator = CreateValidator(new IdeLaunchClaims(
            "student1",
            SiteId: 1,
            ProblemId: 1000,
            ContestId: null,
            Num: null,
            AllowedLanguages: Array.Empty<int>()));

        var publicService = new Mock<IPublicService>();
        publicService
            .Setup(item => item.GetSubmissionStatusAsync(It.IsAny<CurrentUser>(), 321))
            .ReturnsAsync(new PublicSubmissionStatusResponse
            {
                SolutionId = 321,
                ResultCode = JudgeResultCodes.CompileError,
                StatusKey = "compile_error",
                StatusLabel = "Compile Error",
                GeneralStatusKey = "finished",
                RuntimeMessage = "runtime text",
                CompileMessage = "syntax error",
                TimeMs = 0,
                MemoryKb = 0,
                IsFinal = true
            });

        var customInputRepository = new Mock<IIdeCustomInputRepository>();
        customInputRepository.Setup(item => item.IsCustomInputAsync(321)).ReturnsAsync(false);
        var service = new IdeSubmissionService(validator.Object, publicService.Object, customInputRepository.Object);

        var response = await service.GetStatusAsync("launch-token", 321);

        Assert.Equal("completed", response.Phase);
        Assert.Equal("Compilation Error", response.Verdict);
        Assert.Equal(string.Empty, response.Stdout);
        Assert.Equal("runtime text", response.Stderr);
        Assert.Equal("syntax error", response.CompileErrors);
    }

    private static Mock<IIdeLaunchTokenValidator> CreateValidator(IdeLaunchClaims claims)
    {
        var validator = new Mock<IIdeLaunchTokenValidator>();
        validator
            .Setup(item => item.Validate("launch-token"))
            .Returns(claims);

        return validator;
    }
}
