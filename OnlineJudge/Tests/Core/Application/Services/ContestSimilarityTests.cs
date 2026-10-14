using System.IO.Compression;
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
    public async Task Submissions_GoByUserAndAcceptedOnesAlsoByProblem()
    {
        _contests.Setup(item => item.GetContestByIdAsync(7, 3)).ReturnsAsync(new Contest { ContestId = 7 });
        _contests.Setup(item => item.GetSubmissionsForExportAsync(7, 3)).ReturnsAsync(
        [
            new(1, "ana", "Ana, B", 0, 10, "C++", JudgeResultCodes.WrongAnswer, 0, 5, 100, "10.0.0.1", new DateTime(2026, 10, 1, 9, 0, 0), "wa"),
            new(2, "ana", "Ana, B", 0, 10, "C++", JudgeResultCodes.Accepted, 100, 5, 100, "10.0.0.1", new DateTime(2026, 10, 1, 9, 5, 0), "ac"),
            new(3, "luis/x", "Luis", 1, 20, "Python 3", JudgeResultCodes.Accepted, 100, 5, 100, "10.0.0.2", new DateTime(2026, 10, 1, 9, 6, 0), "py"),
        ]);

        using var archive = new ZipArchive(new MemoryStream(await Service().ExportSubmissionsAsync(7, 3)));
        string Read(string path) => new StreamReader(archive.GetEntry(path)!.Open()).ReadToEnd();

        Assert.Equal("wa", Read("users/ana/all/A-1-wrong_answer.cpp"));
        Assert.Equal("ac", Read("users/ana/accepted/A-2.cpp"));
        Assert.Null(archive.GetEntry("users/ana/accepted/A-1.cpp"));
        Assert.Equal("ac", Read("problems/A/ana-2.cpp"));
        Assert.Equal("py", Read("problems/B/luis_x-3.py"));
        var csv = Read("submissions.csv").Split('\n');
        Assert.Equal("1,ana,\"Ana, B\",A,10,C++,wrong_answer,0,5,100,10.0.0.1,2026-10-01 09:00:00,users/ana/all/A-1-wrong_answer.cpp", csv[1]);
    }

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
