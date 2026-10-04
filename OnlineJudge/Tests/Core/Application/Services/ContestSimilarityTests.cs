using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
using static RepositoryTestSupport;

public class ContestSimilarityTests
{
    private readonly Mock<IContestsRepository> _contests = new();
    private readonly Mock<IFileSystemLocalManagerManager> _files = new();

    private ContestSimilarityService Service() => new(_contests.Object, _files.Object);

    [Fact]
    public async Task Repository_ReturnsContestPairsOfDifferentUsersOnly()
    {
        using var seed = new JudgeSeed();
        var alice = seed.Solution("alice", 10, 4, contestId: 7, num: 1);
        var aliceAgain = seed.Solution("alice", 10, 4, contestId: 7, num: 1);
        var bob = seed.Solution("bob", 10, 4, contestId: 7, num: 1);
        var otherContest = seed.Solution("carol", 10, 4, contestId: 8);
        var otherSite = seed.Solution("dave", 10, 4, contestId: 7, siteId: 2);
        seed.Db.SimilarCodes.AddRange(
            new DbSimilarCode { SolutionId = bob, SimilarSId = alice, Percentage = 93 },
            new DbSimilarCode { SolutionId = aliceAgain, SimilarSId = alice, Percentage = 100 },
            new DbSimilarCode { SolutionId = otherContest, SimilarSId = alice, Percentage = 80 },
            new DbSimilarCode { SolutionId = otherSite, SimilarSId = alice, Percentage = 80 });
        seed.Save();

        var rows = await new ContestsRepository(seed.Db, CreateMapper()).GetSimilarityAsync(7, 1);

        Assert.Equal(new[] { new ContestSimilarityItem(10, 1, bob, "bob", alice, "alice", 93) }, rows);
    }

    [Fact]
    public async Task Get_ReportsEachPairOnceAndWhetherARunIsPending()
    {
        _contests.Setup(item => item.GetContestByIdAsync(7, 1)).ReturnsAsync(new Contest { ContestId = 7 });
        _contests.Setup(item => item.GetSimilarityAsync(7, 1)).ReturnsAsync(new[]
        {
            new ContestSimilarityItem(10, 0, 1, "alice", 2, "bob", 80),
            new ContestSimilarityItem(10, 0, 2, "bob", 1, "alice", 80),
            new ContestSimilarityItem(11, 1, 3, "carol", 4, "dave", 95),
        });
        _files.Setup(item => item.ListFiles(Path.Combine("contests", "7"))).Returns(new[] { "similarity.request" });

        var response = await Service().GetAsync(7, 1);

        Assert.True(response.Running);
        Assert.Equal(new[] { 3, 1 }, response.Items.Select(item => item.SolutionId));
    }

    [Fact]
    public async Task RequestRun_LeavesTheRequestFileOnlyForContestsOfTheSite()
    {
        _contests.Setup(item => item.GetContestByIdAsync(7, 1)).ReturnsAsync(new Contest { ContestId = 7 });

        await Service().RequestRunAsync(7, 1);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service().RequestRunAsync(9, 1));

        _files.Verify(item => item.WriteToFile(Path.Combine("contests", "7"), "similarity.request", string.Empty), Times.Once);
        _files.Verify(item => item.WriteToFile(Path.Combine("contests", "9"), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
