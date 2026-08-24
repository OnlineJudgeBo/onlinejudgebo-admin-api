using FluentValidation;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;

public class ProblemServiceTests
{
    [Fact]
    public async Task GetAllProblemsAsync_UsesAdminRepositoryForPrivilegedRoles()
    {
        var repository = new Mock<IProblemRepository>();
        repository.Setup(item => item.GetAllProblemsForAdminAsync(1)).ReturnsAsync(new[] { new Problem { ProblemId = 1 } });
        var service = CreateService(repository.Object);

        var result = await service.GetAllProblemsAsync(new CurrentUser { UserId = "teacher", SiteId = 1, Role = UserRolesEnum.Docente });

        Assert.Single(result);
        repository.Verify(item => item.GetAllProblemsForAdminAsync(1), Times.Once);
        repository.Verify(item => item.GetAllProblemsAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetAllProblemsAsync_UsesPublicRepositoryForInvitadoRole()
    {
        var repository = new Mock<IProblemRepository>();
        repository.Setup(item => item.GetAllProblemsAsync(1)).ReturnsAsync(new[] { new Problem { ProblemId = 2 } });
        var service = CreateService(repository.Object);

        var result = await service.GetAllProblemsAsync(new CurrentUser { UserId = "student", SiteId = 1, Role = UserRolesEnum.Invitado });

        Assert.Single(result);
        repository.Verify(item => item.GetAllProblemsAsync(1), Times.Once);
        repository.Verify(item => item.GetAllProblemsForAdminAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SearchProblemAsync_UsesRoleSpecificRepositoryMethod()
    {
        var repository = new Mock<IProblemRepository>();
        repository.Setup(item => item.SearchProblemForAdminAsync("dp", 1)).ReturnsAsync(new[] { new Problem { ProblemId = 3 } });
        var service = CreateService(repository.Object);

        var result = await service.SearchProblemAsync(new CurrentUser { UserId = "admin", SiteId = 1, Role = UserRolesEnum.Administrador }, "dp");

        Assert.Single(result);
        repository.Verify(item => item.SearchProblemForAdminAsync("dp", 1), Times.Once);
        repository.Verify(item => item.SearchProblemAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateProblemAsync_NormalizesOriginSource_PersistsSampleFiles_AndCreatesPrivilege()
    {
        var repository = new Mock<IProblemRepository>();
        repository
            .Setup(item => item.CreateProblemAsync(It.IsAny<Problem>(), 1))
            .ReturnsAsync((Problem problem, int _) =>
            {
                problem.ProblemId = 1000;
                return problem;
            });

        var topicRepository = new Mock<ITopicRepository>();
        var privilegeRepository = new Mock<IPrivilegeRepository>();
        var fileSystem = new Mock<IFileSystemLocalManagerManager>();
        var service = CreateService(repository.Object, topicRepository.Object, privilegeRepository.Object, fileSystem.Object);
        var classifications = new[] { new Classification { ClassificationId = 7 } };

        var result = await service.CreateProblemAsync("teacher", new Problem
        {
            Title = "Suma",
            OriginSource = "  OBI   2024  ",
            SampleInput = "1 2",
            SampleOutput = "3",
            Classifications = classifications
        }, 1);

        Assert.Equal(1000, result.ProblemId);
        Assert.Equal("OBI 2024", result.OriginSource);
        Assert.Null(result.Classifications);
        topicRepository.Verify(item => item.AddClassificationsToProblemAsync(1000, classifications), Times.Once);
        fileSystem.Verify(item => item.CreateFolder("1000"), Times.Once);
        fileSystem.Verify(item => item.WriteToFile("1000", "sample.in", "1 2"), Times.Once);
        fileSystem.Verify(item => item.WriteToFile("1000", "sample.out", "3"), Times.Once);
        privilegeRepository.Verify(item => item.CreatePrivilegeAsync(It.Is<Privilege>(privilege => privilege.UserId == "teacher" && privilege.Rightstr == "p1000")), Times.Once);
    }

    [Fact]
    public async Task UpdateProblemAsync_ThrowsWhenUserIdIsEmpty()
    {
        var repository = new Mock<IProblemRepository>();
        repository.Setup(item => item.GetProblemByIdAsync(1000, 1)).ReturnsAsync(new Problem { ProblemId = 1000 });
        var service = CreateService(repository.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.UpdateProblemAsync(string.Empty, 1000, new Problem(), 1));
    }

    [Fact]
    public async Task UpdateProblemAsync_ThrowsWhenProblemDoesNotExist()
    {
        var repository = new Mock<IProblemRepository>();
        repository.Setup(item => item.GetProblemByIdAsync(1000, 1)).ReturnsAsync((Problem)null!);
        var service = CreateService(repository.Object);

        var error = await Assert.ThrowsAsync<ApplicationException>(() => service.UpdateProblemAsync("teacher", 1000, new Problem(), 1));

        Assert.Equal("Problem does not exist.", error.Message);
    }

    [Fact]
    public async Task UpdateProblemAsync_PassesCallersSiteIdToRepository()
    {
        var repository = new Mock<IProblemRepository>();
        repository.Setup(item => item.GetProblemByIdAsync(1000, 2)).ReturnsAsync((Problem)null!);
        var service = CreateService(repository.Object);

        await Assert.ThrowsAsync<ApplicationException>(() => service.UpdateProblemAsync("teacher", 1000, new Problem(), 2));

        repository.Verify(item => item.GetProblemByIdAsync(1000, 2), Times.Once);
        repository.Verify(item => item.UpdateProblemAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Problem>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ChangeProblemVisibilityAsync_PassesCallersSiteIdToRepository()
    {
        var repository = new Mock<IProblemRepository>();
        var service = CreateService(repository.Object);

        await service.ChangeProblemVisibilityAsync(1000, 2);

        repository.Verify(item => item.ChangeProblemVisibilityAsync(1000, 2), Times.Once);
    }

    [Fact]
    public async Task UpdateProblemAsync_ReplacesClassificationsAndSampleFiles()
    {
        var repository = new Mock<IProblemRepository>();
        repository.Setup(item => item.GetProblemByIdAsync(1000, 1)).ReturnsAsync(new Problem { ProblemId = 1000 });
        repository.Setup(item => item.UpdateProblemAsync("teacher", 1000, It.IsAny<Problem>(), 1))
            .ReturnsAsync((string _, int _, Problem problem, int _) =>
            {
                problem.ProblemId = 1000;
                return problem;
            });

        var topicRepository = new Mock<ITopicRepository>();
        var fileSystem = new Mock<IFileSystemLocalManagerManager>();
        var service = CreateService(repository.Object, topicRepository.Object, fileSystemLocalManager: fileSystem.Object);
        var classifications = new[] { new Classification { ClassificationId = 8 } };

        var result = await service.UpdateProblemAsync("teacher", 1000, new Problem
        {
            OriginSource = null,
            SampleInput = "in",
            SampleOutput = "out",
            Classifications = classifications
        }, 1);

        Assert.Equal("General", result.OriginSource);
        topicRepository.Verify(item => item.RemoveAllClassificationsFromProblemAsync(1000), Times.Once);
        topicRepository.Verify(item => item.AddClassificationsToProblemAsync(1000, classifications), Times.Once);
        fileSystem.Verify(item => item.CreateFolder("1000"), Times.Once);
        fileSystem.Verify(item => item.CreateFolder("1000/ac"), Times.Once);
        fileSystem.Verify(item => item.WriteToFile("1000", "sample.in", "in"), Times.Once);
        fileSystem.Verify(item => item.WriteToFile("1000", "sample.out", "out"), Times.Once);
    }

    [Fact]
    public async Task UpdateProblemAsync_WritesOneFilePairPerSampleCase()
    {
        var repository = new Mock<IProblemRepository>();
        repository.Setup(item => item.GetProblemByIdAsync(1000, 1)).ReturnsAsync(new Problem { ProblemId = 1000 });
        repository.Setup(item => item.UpdateProblemAsync("teacher", 1000, It.IsAny<Problem>(), 1))
            .ReturnsAsync((string _, int _, Problem problem, int _) => { problem.ProblemId = 1000; return problem; });

        var fileSystem = new Mock<IFileSystemLocalManagerManager>();
        var service = CreateService(repository.Object, fileSystemLocalManager: fileSystem.Object);

        await service.UpdateProblemAsync("teacher", 1000, new Problem
        {
            SampleCases = new List<ProblemSample>
            {
                new() { Input = "one-in", Output = "one-out" },
                new() { Input = "two-in", Output = "two-out" },
            }
        }, 1);

        fileSystem.Verify(item => item.WriteToFile("1000", "sample.in", "one-in"), Times.Once);
        fileSystem.Verify(item => item.WriteToFile("1000", "sample.out", "one-out"), Times.Once);
        fileSystem.Verify(item => item.WriteToFile("1000", "sample-2.in", "two-in"), Times.Once);
        fileSystem.Verify(item => item.WriteToFile("1000", "sample-2.out", "two-out"), Times.Once);
    }

    [Fact]
    public async Task UpdateProblemAsync_DeletesStaleSampleFilesWhenSampleCountShrinks()
    {
        var repository = new Mock<IProblemRepository>();
        repository.Setup(item => item.GetProblemByIdAsync(1000, 1)).ReturnsAsync(new Problem { ProblemId = 1000 });
        repository.Setup(item => item.UpdateProblemAsync("teacher", 1000, It.IsAny<Problem>(), 1))
            .ReturnsAsync((string _, int _, Problem problem, int _) => { problem.ProblemId = 1000; return problem; });

        var fileSystem = new Mock<IFileSystemLocalManagerManager>();
        fileSystem.Setup(item => item.ListFiles("1000"))
            .Returns(new List<string> { "sample.in", "sample.out", "sample-2.in", "sample-2.out", "sample-3.in", "sample-3.out", "1.in" });
        var service = CreateService(repository.Object, fileSystemLocalManager: fileSystem.Object);

        await service.UpdateProblemAsync("teacher", 1000, new Problem
        {
            SampleCases = new List<ProblemSample> { new() { Input = "only-in", Output = "only-out" } }
        }, 1);

        fileSystem.Verify(item => item.DeleteFile("1000", "sample-2.in"), Times.Once);
        fileSystem.Verify(item => item.DeleteFile("1000", "sample-2.out"), Times.Once);
        fileSystem.Verify(item => item.DeleteFile("1000", "sample-3.in"), Times.Once);
        fileSystem.Verify(item => item.DeleteFile("1000", "sample-3.out"), Times.Once);
        fileSystem.Verify(item => item.DeleteFile("1000", "1.in"), Times.Never);
    }

    private static ProblemService CreateService(
        IProblemRepository? problemRepository = null,
        ITopicRepository? topicRepository = null,
        IPrivilegeRepository? privilegeRepository = null,
        IFileSystemLocalManagerManager? fileSystemLocalManager = null)
    {
        return new ProblemService(
            problemRepository ?? Mock.Of<IProblemRepository>(),
            topicRepository ?? Mock.Of<ITopicRepository>(),
            privilegeRepository ?? Mock.Of<IPrivilegeRepository>(),
            fileSystemLocalManager ?? Mock.Of<IFileSystemLocalManagerManager>(),
            Mock.Of<IValidator<Problem>>());
    }
}
