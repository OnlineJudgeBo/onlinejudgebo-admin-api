using FluentValidation;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;

public class ContestServiceTests
{
    [Theory]
    [InlineData(UserRolesEnum.Administrador, true)]
    [InlineData(UserRolesEnum.Docente, true)]
    public async Task GetAllContestAsync_UsesDocenteRepositoryWithExpectedShowAllFlag(UserRolesEnum role, bool showAll)
    {
        var contestRepository = new Mock<IContestsRepository>();
        contestRepository
            .Setup(item => item.GetContestsByUserIdDocenteRoleAsync("user1", showAll, 1))
            .ReturnsAsync(new[] { new Contest { ContestId = 1 } });
        var service = CreateService(contestRepository.Object);

        var result = await service.GetAllContestAsync(new CurrentUser { UserId = "user1", SiteId = 1, Role = role });

        Assert.Single(result);
        contestRepository.Verify(item => item.GetContestsByUserIdDocenteRoleAsync("user1", showAll, 1), Times.Once);
    }

    [Fact]
    public async Task GetAllContestAsync_UsesAuxiliarRepositoryForAuxiliarRole()
    {
        var contestRepository = new Mock<IContestsRepository>();
        contestRepository.Setup(item => item.GetContestsByAuxiliarRoleAsync("aux", 1)).ReturnsAsync(new[] { new Contest { ContestId = 2 } });
        var service = CreateService(contestRepository.Object);

        var result = await service.GetAllContestAsync(new CurrentUser { UserId = "aux", SiteId = 1, Role = UserRolesEnum.Auxiliar });

        Assert.Single(result);
        contestRepository.Verify(item => item.GetContestsByAuxiliarRoleAsync("aux", 1), Times.Once);
    }

    [Fact]
    public async Task CreateContestAsync_DefaultsMetadata_NumeratesProblems_AndDeduplicatesValidUsers()
    {
        Contest? capturedContest = null;
        var contestRepository = new Mock<IContestsRepository>();
        contestRepository
            .Setup(item => item.CreateContestAsync(It.IsAny<Contest>(), 1))
            .Callback<Contest, int>((contest, _) => capturedContest = contest)
            .ReturnsAsync(new Contest { ContestId = 10 });
        contestRepository.Setup(item => item.GetContestByIdAsync(10, 1)).ReturnsAsync(new Contest { ContestId = 10 });

        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(item => item.GetUserById("owner", 1)).ReturnsAsync(new User { UserId = "owner" });
        userRepository.Setup(item => item.GetUserById("student", 1)).ReturnsAsync(new User { UserId = "student" });
        userRepository.Setup(item => item.GetUserById("missing", 1)).ReturnsAsync((User)null!);
        var service = CreateService(contestRepository.Object, userRepository: userRepository.Object);

        var result = await service.CreateContestAsync("owner", new Contest
        {
            Track = string.Empty,
            Level = string.Empty,
            ContestProblems = new List<ContestProblem>
            {
                new ContestProblem { ProblemId = 1000 },
                new ContestProblem { ProblemId = 1001 }
            },
            ContestUsers = new List<ContestUser>
            {
                new ContestUser { UserId = "student" }
            }
        }, "student" + Environment.NewLine + "missing", 1);

        Assert.Equal(10, result.ContestId);
        Assert.NotNull(capturedContest);
        Assert.Equal("GENERAL", capturedContest!.Track);
        Assert.Equal("PRACTICE", capturedContest.Level);
        Assert.Equal(new int?[] { 0, 1 }, capturedContest.ContestProblems.Select(problem => problem.Num).ToArray());
        Assert.Equal(new[] { "owner", "student" }, capturedContest.ContestUsers!.Select(user => user.UserId).OrderBy(userId => userId).ToArray());
        Assert.True(capturedContest.ContestUsers!.Single(user => user.UserId == "owner").IsOwner);
    }

    [Fact]
    public async Task CreateContestAsync_RejectsInvalidTrack()
    {
        var service = CreateService();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateContestAsync("owner", new Contest
        {
            Track = "UNKNOWN",
            Level = "PRACTICE"
        }, string.Empty, 1));

        Assert.Equal("Contest track must be OBI, ICPC_BOLIVIA or GENERAL.", error.Message);
    }

    [Fact]
    public async Task UpdateContestAsync_ThrowsWhenExistingContestDoesNotExist()
    {
        var contestRepository = new Mock<IContestsRepository>();
        contestRepository.Setup(item => item.GetContestByIdAsync(10, 1)).ReturnsAsync((Contest)null!);
        var service = CreateService(contestRepository.Object);

        var error = await Assert.ThrowsAsync<ApplicationException>(() => service.UpdateContestAsync(10, new Contest(), string.Empty, 1));

        Assert.Equal("Contest does not exist.", error.Message);
    }

    [Fact]
    public async Task GetContestById_PassesCallersSiteIdToRepository()
    {
        var contestRepository = new Mock<IContestsRepository>();
        contestRepository.Setup(item => item.GetContestByIdAsync(10, 2)).ReturnsAsync((Contest)null!);
        var service = CreateService(contestRepository.Object);

        var result = await service.GetContestById(10, 2);

        Assert.Null(result);
        contestRepository.Verify(item => item.GetContestByIdAsync(10, 2), Times.Once);
    }

    [Fact]
    public async Task PromoteContestAsync_RejectsContestFromAnotherSite()
    {
        var contestRepository = new Mock<IContestsRepository>();
        contestRepository.Setup(item => item.GetContestByIdAsync(10, 2)).ReturnsAsync((Contest)null!);
        var service = CreateService(contestRepository.Object);

        await Assert.ThrowsAsync<ApplicationException>(() => service.PromoteContestAsync(10, 2));

        contestRepository.Verify(item => item.GetContestByIdAsync(10, 2), Times.Once);
        contestRepository.Verify(item => item.PromoteContestAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }


    [Fact]
    public async Task UpdateContestAsync_ReplacesContestUsersWithSubmittedValidUsers()
    {
        Contest? capturedContest = null;
        var contestRepository = new Mock<IContestsRepository>();
        contestRepository.Setup(item => item.GetContestByIdAsync(10, 1)).ReturnsAsync(new Contest { ContestId = 10 });
        contestRepository
            .Setup(item => item.UpdateContestAsync(10, It.IsAny<Contest>(), 1))
            .Callback<int, Contest, int>((_, contest, _) => capturedContest = contest)
            .ReturnsAsync(new Contest { ContestId = 10 });

        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(item => item.GetUserById("keep", 1)).ReturnsAsync(new User { UserId = "keep" });
        userRepository.Setup(item => item.GetUserById("alsoKeep", 1)).ReturnsAsync(new User { UserId = "alsoKeep" });
        userRepository.Setup(item => item.GetUserById("removed", 1)).ReturnsAsync(new User { UserId = "removed" });
        userRepository.Setup(item => item.GetUserById("missing", 1)).ReturnsAsync((User)null!);

        var service = CreateService(contestRepository.Object, userRepository: userRepository.Object);

        await service.UpdateContestAsync(10, new Contest
        {
            Track = "GENERAL",
            Level = "PRACTICE",
            ContestProblems = new List<ContestProblem>(),
            ContestUsers = new List<ContestUser>
            {
                new ContestUser { UserId = "removed" }
            }
        }, " keep, alsoKeep\nmissing ", 1);

        Assert.NotNull(capturedContest);
        Assert.Equal(new[] { "alsoKeep", "keep" }, capturedContest!.ContestUsers!.Select(user => user.UserId).OrderBy(userId => userId).ToArray());
        Assert.DoesNotContain(capturedContest.ContestUsers!, user => user.UserId == "removed");
        Assert.All(capturedContest.ContestUsers!, user => Assert.Equal(1, user.SiteId));
    }

    [Fact]
    public async Task PromoteContestAsync_PromotesContestAndItsProblems()
    {
        var contestRepository = new Mock<IContestsRepository>();
        contestRepository.Setup(item => item.GetContestByIdAsync(10, 1)).ReturnsAsync(new Contest
        {
            ContestId = 10,
            ContestProblems = new List<ContestProblem>
            {
                new ContestProblem { ProblemId = 1000 },
                new ContestProblem { ProblemId = 1001 }
            }
        });
        var problemRepository = new Mock<IProblemRepository>();
        var service = CreateService(contestRepository.Object, problemRepository.Object);

        await service.PromoteContestAsync(10, 1);

        contestRepository.Verify(item => item.PromoteContestAsync(10, 1), Times.Once);
        problemRepository.Verify(item => item.PromoteProblemAsync(It.Is<List<int>>(ids => ids.SequenceEqual(new[] { 1000, 1001 }))), Times.Once);
    }

    private static ContestService CreateService(
        IContestsRepository? contestRepository = null,
        IProblemRepository? problemRepository = null,
        IPrivilegeRepository? privilegeRepository = null,
        IUserRepository? userRepository = null)
    {
        return new ContestService(
            contestRepository ?? Mock.Of<IContestsRepository>(),
            problemRepository ?? Mock.Of<IProblemRepository>(),
            privilegeRepository ?? Mock.Of<IPrivilegeRepository>(),
            userRepository ?? Mock.Of<IUserRepository>(),
            Mock.Of<IValidator<Problem>>());
    }
}
