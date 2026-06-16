using OnlineJudgeAdmin.Core.Application.Services.Implementations.IdeIntegration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;

public class IdeContextServiceTests
{
    [Fact]
    public async Task BuildContextAsync_ReturnsUserNotAllowedWhenLaunchUserDoesNotExist()
    {
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(item => item.GetUserById("student1", 1))
            .ReturnsAsync((User)null!);

        var service = new IdeContextService(
            Mock.Of<IProblemService>(),
            userRepository.Object,
            Mock.Of<IIdeLanguageDefinitionService>());

        var result = await service.BuildContextAsync(CreateClaims(), new IdeContextRequest(1000, 2, "Python", "handoff"));

        Assert.Equal(IdeContextBuildStatus.UserNotAllowed, result.Status);
        Assert.Null(result.Response);
    }

    [Fact]
    public async Task BuildContextAsync_ReturnsProblemNotFoundWhenProblemDoesNotExist()
    {
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(item => item.GetUserById("student1", 1))
            .ReturnsAsync(new User { UserId = "student1" });

        var problemService = new Mock<IProblemService>();
        problemService
            .Setup(item => item.GetProblemByIdAsync(1000, null))
            .ReturnsAsync((Problem)null!);

        var service = new IdeContextService(
            problemService.Object,
            userRepository.Object,
            Mock.Of<IIdeLanguageDefinitionService>());

        var result = await service.BuildContextAsync(CreateClaims(), new IdeContextRequest(1000, 2, "Python", "handoff"));

        Assert.Equal(IdeContextBuildStatus.ProblemNotFound, result.Status);
        Assert.Null(result.Response);
    }

    [Fact]
    public async Task BuildContextAsync_ReturnsProblemContextAndTokenIdentifiers()
    {
        var claims = new IdeLaunchClaims(
            "student1",
            SiteId: 1,
            ProblemId: 1000,
            ContestId: 3040,
            Num: 0,
            AllowedLanguages: new[] { 2, 3 });

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(item => item.GetUserById("student1", 1))
            .ReturnsAsync(new User { UserId = "student1" });

        var problemService = new Mock<IProblemService>();
        problemService
            .Setup(item => item.GetProblemByIdAsync(1000, null))
            .ReturnsAsync(new Problem
            {
                ProblemId = 1000,
                Title = "Suma",
                Description = "Suma dos numeros",
                Input = "a b",
                Output = "a+b",
                SampleInput = "1 2",
                SampleOutput = "3",
                Hint = "Usa enteros",
                TimeLimit = 1,
                MemoryLimit = 128
            });

        int[]? capturedAllowedLanguages = null;
        var languageDefinitionService = new Mock<IIdeLanguageDefinitionService>();
        languageDefinitionService
            .Setup(item => item.GetAllowedLanguageDefinitionsAsync(It.IsAny<int[]>()))
            .Callback<int[]>(allowedLanguages => capturedAllowedLanguages = allowedLanguages)
            .ReturnsAsync(new[] { new IdeLanguageDefinition(2, "Python 3", "python") });

        var service = new IdeContextService(problemService.Object, userRepository.Object, languageDefinitionService.Object);

        var result = await service.BuildContextAsync(claims, new IdeContextRequest(1000, 2, "Python", "handoff-data"));

        Assert.Equal(IdeContextBuildStatus.Success, result.Status);
        Assert.NotNull(result.Response);
        Assert.Equal(new[] { 2, 3 }, result.Response!.AllowedLanguages);
        Assert.Equal(new[] { 2, 3 }, capturedAllowedLanguages);
        Assert.Equal("1000", result.Response.Problem.ProblemId);
        Assert.Equal("Suma", result.Response.Problem.Title);
        Assert.Equal("Suma dos numeros", result.Response.Problem.Description);
        Assert.Equal("a b", result.Response.Problem.Input);
        Assert.Equal("a+b", result.Response.Problem.Output);
        Assert.Equal("1 2", result.Response.Problem.Example.Input);
        Assert.Equal("3", result.Response.Problem.Example.Output);
        Assert.Equal("Usa enteros", result.Response.Problem.Hints);
        Assert.Equal("1s", result.Response.Problem.TimeLimit);
        Assert.Equal("128 MB", result.Response.Problem.MemoryLimit);
        Assert.Equal(1000, result.Response.Identifiers.ProblemId);
        Assert.Equal(3040, result.Response.Identifiers.ContestId);
        Assert.Equal(0, result.Response.Identifiers.Num);
        Assert.Equal("student1", result.Response.Identifiers.UserId);
        Assert.Equal(1, result.Response.Identifiers.SiteId);
        Assert.Equal(2, result.Response.Identifiers.LanguageId);
        Assert.Equal("Python", result.Response.Identifiers.LanguageName);
        Assert.Equal("handoff-data", result.Response.Identifiers.Handoff);
    }

    [Fact]
    public async Task BuildContextAsync_UsesRequestedLanguageWhenTokenDoesNotRestrictLanguages()
    {
        var claims = new IdeLaunchClaims(
            "student1",
            SiteId: 1,
            ProblemId: 1000,
            ContestId: null,
            Num: null,
            AllowedLanguages: Array.Empty<int>());

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(item => item.GetUserById("student1", 1))
            .ReturnsAsync(new User { UserId = "student1" });

        var problemService = new Mock<IProblemService>();
        problemService
            .Setup(item => item.GetProblemByIdAsync(1000, null))
            .ReturnsAsync(new Problem { ProblemId = 1000 });

        int[]? capturedAllowedLanguages = null;
        var languageDefinitionService = new Mock<IIdeLanguageDefinitionService>();
        languageDefinitionService
            .Setup(item => item.GetAllowedLanguageDefinitionsAsync(It.IsAny<int[]>()))
            .Callback<int[]>(allowedLanguages => capturedAllowedLanguages = allowedLanguages)
            .ReturnsAsync(Array.Empty<IdeLanguageDefinition>());

        var service = new IdeContextService(problemService.Object, userRepository.Object, languageDefinitionService.Object);

        var result = await service.BuildContextAsync(claims, new IdeContextRequest(1000, 5, "Java", null));

        Assert.Equal(IdeContextBuildStatus.Success, result.Status);
        Assert.Equal(new[] { 5 }, result.Response!.AllowedLanguages);
        Assert.Equal(new[] { 5 }, capturedAllowedLanguages);
        Assert.Equal("Problema 1000", result.Response.Problem.Title);
        Assert.Equal(string.Empty, result.Response.Problem.Description);
    }

    private static IdeLaunchClaims CreateClaims()
    {
        return new IdeLaunchClaims(
            "student1",
            SiteId: 1,
            ProblemId: 1000,
            ContestId: null,
            Num: null,
            AllowedLanguages: Array.Empty<int>());
    }
}
