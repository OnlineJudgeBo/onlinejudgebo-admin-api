using FluentValidation;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

public class ApplicationServiceTests
{
    [Fact]
    public async Task ProgrammingLanguageService_ReturnsRepositoryLanguages()
    {
        var repository = new Mock<IProgrammingLanguagesRepository>();
        repository.Setup(item => item.GetAllProgrammingLanguageAsync()).ReturnsAsync(new[] { new ProgrammingLanguage { LanguageId = 1 } });
        var service = new ProgrammingLanguageService(repository.Object, Mock.Of<IValidator<Problem>>());

        var result = await service.GetAllProgrammingLanguageAsync();

        Assert.Single(result);
        repository.Verify(item => item.GetAllProgrammingLanguageAsync(), Times.Once);
    }

    [Fact]
    public async Task RoleService_DelegatesRoleOperationsToRepository()
    {
        var repository = new Mock<IRoleRepository>();
        repository.Setup(item => item.GetAllRolesAsync()).ReturnsAsync(new[] { new Role { RoleId = 1 } });
        repository.Setup(item => item.GetUserRolesAsync(1)).ReturnsAsync(new[] { new User { UserId = "u1" } });
        var service = new RoleService(repository.Object, Mock.Of<IValidator<Problem>>());

        Assert.Single(await service.GetAllRolesAsync());
        Assert.Single(await service.GetUserRolesAsync(1));
        await service.AddRoleToUserAsync("u1", 2, 1);
        await service.RemoveRoleFromUserAsync("u1", 2, 1);

        repository.Verify(item => item.AddRoleToUserAsync("u1", 2, 1), Times.Once);
        repository.Verify(item => item.RemoveRoleFromUserAsync("u1", 2, 1), Times.Once);
    }

    [Fact]
    public async Task StaticsServices_DelegatesStatisticsQueriesToRepository()
    {
        var repository = new Mock<IStaticsRepository>();
        repository.Setup(item => item.GetLast365DaysSubmissionsByMonthAsync(1)).ReturnsAsync("months");
        repository.Setup(item => item.GetSubmissionsByLanguageAsync(1)).ReturnsAsync("languages");
        var service = new StaticsServices(Mock.Of<IPrivilegeRepository>(), repository.Object, Mock.Of<IValidator<Problem>>());

        Assert.Equal("months", await service.GetLast365DaysSubmissionsByMonthAsync(1));
        Assert.Equal("languages", await service.GetSubmissionsByLanguageAsync(1));
    }

    [Fact]
    public async Task TopicService_AddClassificationToTopic_RejectsEmptyClassifications()
    {
        var service = new TopicService(Mock.Of<ITopicRepository>(), Mock.Of<IValidator<Problem>>());

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.AddClassificationToTopic(1, new Topic()));

        Assert.Equal("No classifications provided.", error.Message);
    }

    [Fact]
    public async Task TopicService_UpdateClassification_ThrowsWhenClassificationDoesNotExist()
    {
        var repository = new Mock<ITopicRepository>();
        repository.Setup(item => item.GetClassificationById(10)).ReturnsAsync((Classification)null!);
        var service = new TopicService(repository.Object, Mock.Of<IValidator<Problem>>());

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateClassification(new Classification(), 10));

        Assert.Equal("Classification not found.", error.Message);
        repository.Verify(item => item.UpdateClassification(It.IsAny<Classification>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task TopicService_UpdateClassification_DelegatesWhenClassificationExists()
    {
        var repository = new Mock<ITopicRepository>();
        repository.Setup(item => item.GetClassificationById(10)).ReturnsAsync(new Classification { ClassificationId = 10 });
        var service = new TopicService(repository.Object, Mock.Of<IValidator<Problem>>());
        var classification = new Classification { ClassificationId = 11 };

        await service.UpdateClassification(classification, 10);

        repository.Verify(item => item.UpdateClassification(classification, 10), Times.Once);
    }

    [Fact]
    public async Task FileManagerService_S3UploadFileAsync_UsesConfiguredBucketAndGeneratedKey()
    {
        string? capturedBucket = null;
        string? capturedKey = null;
        var awsS3 = new Mock<IAwsS3FileManager>();
        awsS3
            .Setup(item => item.S3UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), "solution.zip"))
            .Callback<string, string, string>((bucket, key, _) =>
            {
                capturedBucket = bucket;
                capturedKey = key;
            })
            .ReturnsAsync("https://files/solution.zip");
        var bucketSection = new Mock<IConfigurationSection>();
        bucketSection.SetupGet(item => item.Value).Returns("judge-bucket");
        var configuration = new Mock<IConfiguration>();
        configuration.Setup(item => item.GetSection("Base:BucketName")).Returns(bucketSection.Object);
        var service = new FileManagerService(awsS3.Object, Mock.Of<IFileSystemLocalManagerManager>(), configuration.Object, Mock.Of<IValidator<Problem>>());

        var url = await service.S3UploadFileAsync("solution.zip");

        Assert.Equal("https://files/solution.zip", url);
        Assert.Equal("judge-bucket", capturedBucket);
        Assert.False(string.IsNullOrWhiteSpace(capturedKey));
    }

    [Fact]
    public async Task JudgeService_RejudgeMethods_DelegateToRepository()
    {
        var judgeRepository = new Mock<IJudgeRepository>();
        var service = CreateJudgeService(judgeRepository: judgeRepository.Object);

        await service.RejudgeSolutionByIdAsync(123);
        await service.RejudgeSolutionByProblemIdAsync(1000);

        judgeRepository.Verify(item => item.RejudgeSolutionByIdAsync(123), Times.Once);
        judgeRepository.Verify(item => item.RejudgeSolutionByProblemIdAsync(1000), Times.Once);
    }

    [Fact]
    public async Task JudgeService_RemoteExecutionAsync_ThrowsWhenProblemDoesNotExist()
    {
        var problemService = new Mock<IProblemService>();
        problemService.Setup(item => item.GetProblemByIdAsync(1000, null)).ReturnsAsync((Problem)null!);
        var service = CreateJudgeService(problemService: problemService.Object);

        var error = await Assert.ThrowsAsync<Exception>(() => service.RemoteExecutionAsync(new RemoteExecutionRequest { JudgeProblemId = 1000 }, "student", 1));

        Assert.Equal("The problem with ID 1000 is not a valid id.", error.Message);
    }

    [Fact]
    public async Task JudgeService_RemoteExecutionAsync_SavesSolutionSourceAndRemoteLink()
    {
        var problemService = new Mock<IProblemService>();
        problemService.Setup(item => item.GetProblemByIdAsync(1000, null)).ReturnsAsync(new Problem { ProblemId = 1000 });
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(item => item.GetUserById("student", 1)).ReturnsAsync(new User { UserId = "student" });
        var solutionService = new Mock<ISolutionService>();
        solutionService.Setup(item => item.SaveSolutionRemoteAsync("student", 1000, 2, "print(42)", 77)).ReturnsAsync(123);
        var solutionClientRepository = new Mock<ISolutionClientRepository>();
        var service = CreateJudgeService(
            solutionClientRepository.Object,
            userRepository.Object,
            problemService.Object,
            solutionService.Object);

        await service.RemoteExecutionAsync(new RemoteExecutionRequest
        {
            JudgeProblemId = 1000,
            JudgeLanguageId = 2,
            ClientSource = "print(42)",
            ClientSubmitId = 77,
            ClientId = 9
        }, "student", 1);

        solutionClientRepository.Verify(item => item.SaveSourceCodeAsync(123, "print(42)"), Times.Once);
        solutionClientRepository.Verify(item => item.SaveRemoteSolutionAsync(123, 9), Times.Once);
    }

    [Fact]
    public async Task SolutionService_SaveSolutionRemoteAsync_CreatesRemoteSolutionWithExpectedDefaults()
    {
        Solution? capturedSolution = null;
        var repository = new Mock<ISolutionRepository>();
        repository
            .Setup(item => item.SaveSolutionAsync(It.IsAny<Solution>()))
            .Callback<Solution>(solution => capturedSolution = solution)
            .ReturnsAsync(123);
        var service = new SolutionService(repository.Object, Mock.Of<IValidator<Problem>>());

        var id = await service.SaveSolutionRemoteAsync("student", 1000, 2, "print(42)", 77);

        Assert.Equal(123, id);
        Assert.NotNull(capturedSolution);
        Assert.Equal("student", capturedSolution!.UserId);
        Assert.Equal(1000, capturedSolution.ProblemId);
        Assert.Equal(2, capturedSolution.Language);
        Assert.Equal(0, capturedSolution.Time);
        Assert.Equal(0, capturedSolution.Memory);
        Assert.Equal(0, capturedSolution.Result);
        Assert.Equal("0.0.0.0", capturedSolution.Ip);
        Assert.Equal("print(42)".Length, capturedSolution.CodeLength);
        Assert.Equal(77, capturedSolution.RemoteId);
        Assert.True(capturedSolution.IsRemoteOj);
    }

    [Fact]
    public async Task SolutionService_GetSolutionByIdAsync_RejectsInvalidId()
    {
        var service = new SolutionService(Mock.Of<ISolutionRepository>(), Mock.Of<IValidator<Problem>>());

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.GetSolutionByIdAsync(0));

        Assert.Equal("SolutionId inválido.", error.Message);
    }

    [Fact]
    public async Task SolutionService_UpdateSolutionRemoteAsync_RejectsLocalSolution()
    {
        var repository = new Mock<ISolutionRepository>();
        repository.Setup(item => item.GetSolutionByIdAsync(123)).ReturnsAsync(new Solution { SolutionId = 123, IsRemoteOj = false });
        var service = new SolutionService(repository.Object, Mock.Of<IValidator<Problem>>());

        var error = await Assert.ThrowsAsync<Exception>(() => service.UpdateSolutionRemoteAsync(new Solution { SolutionId = 123 }));

        Assert.Equal("You do not own this solution.", error.Message);
    }

    [Fact]
    public async Task SolutionService_UpdateSolutionRemoteAsync_UpdatesAllowedRemoteFields()
    {
        var existing = new Solution { SolutionId = 123, IsRemoteOj = true, RemoteId = 55, InDate = new DateTime(2024, 1, 1) };
        var repository = new Mock<ISolutionRepository>();
        repository.Setup(item => item.GetSolutionByIdAsync(123)).ReturnsAsync(existing);
        var service = new SolutionService(repository.Object, Mock.Of<IValidator<Problem>>());
        var judgeTime = DateTime.UtcNow;

        await service.UpdateSolutionRemoteAsync(new Solution
        {
            SolutionId = 123,
            Time = 10,
            Memory = 2048,
            JudgeTime = judgeTime,
            Result = JudgeResultCodes.Accepted,
            RemoteId = 999
        });

        Assert.Equal(10, existing.Time);
        Assert.Equal(2048, existing.Memory);
        Assert.Equal(judgeTime, existing.JudgeTime);
        Assert.Equal(JudgeResultCodes.Accepted, existing.Result);
        Assert.Equal(55, existing.RemoteId);
        repository.Verify(item => item.UpdateSolutionRemoteAsync(existing), Times.Once);
    }

    private static JudgeService CreateJudgeService(
        ISolutionClientRepository? solutionClientRepository = null,
        IUserRepository? userRepository = null,
        IProblemService? problemService = null,
        ISolutionService? solutionService = null,
        IJudgeRepository? judgeRepository = null)
    {
        return new JudgeService(
            judgeRepository ?? Mock.Of<IJudgeRepository>(),
            solutionClientRepository ?? Mock.Of<ISolutionClientRepository>(),
            userRepository ?? Mock.Of<IUserRepository>(),
            problemService ?? Mock.Of<IProblemService>(),
            solutionService ?? Mock.Of<ISolutionService>(),
            Mock.Of<IValidator<Problem>>());
    }
}
