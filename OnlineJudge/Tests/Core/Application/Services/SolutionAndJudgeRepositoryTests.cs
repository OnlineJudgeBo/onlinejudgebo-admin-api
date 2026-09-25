using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
using static RepositoryTestSupport;

public class SolutionRepositoryTests
{
    private static SolutionRepository Repository(JudgeSeed seed) => new(seed.Db, CreateMapper());

    [Fact]
    public async Task GetSolutionById_IncludesSourceAndCompileInfo()
    {
        using var seed = new JudgeSeed();
        seed.User("ana", nick: "Ana");
        var p = seed.Problem("A");
        var id = seed.Solution("ana", p, 11, source: "int main(){");
        seed.Db.Set<DbCompileinfo>().Add(new DbCompileinfo { SolutionId = id, Error = "expected }" });
        seed.Save();

        var solution = await Repository(seed).GetSolutionByIdAsync(id);

        Assert.NotNull(solution);
        Assert.Equal("ana", solution!.UserId);
        Assert.Equal(11, solution.Result);
        Assert.Null(await Repository(seed).GetSolutionByIdAsync(999));
    }

    [Fact]
    public async Task SaveSolution_ReturnsGeneratedId()
    {
        using var seed = new JudgeSeed();
        var p = seed.Problem("A");

        var id = await Repository(seed).SaveSolutionAsync(new Solution { UserId = "ana", ProblemId = p, Ip = "1.1.1.1", SiteId = 1, InDate = DateTime.Now, IsRemoteOj = true });

        Assert.True(id > 0);
        Assert.True((await seed.Db.Solutions.AsNoTracking().SingleAsync(item => item.SolutionId == id)).IsRemoteOj);
    }

    [Fact]
    public async Task UpdateSolutionRemote_OverwritesVerdictFields()
    {
        using var seed = new JudgeSeed();
        seed.User("ana");
        var p = seed.Problem("A");
        var id = seed.Solution("ana", p, 0);
        var solution = (await Repository(seed).GetSolutionByIdAsync(id))!;
        solution.Result = 4;
        solution.Time = 123;
        solution.Memory = 456;

        await Repository(seed).UpdateSolutionRemoteAsync(solution);

        var stored = await seed.Db.Solutions.AsNoTracking().SingleAsync(item => item.SolutionId == id);
        Assert.Equal(4, stored.Result);
        Assert.Equal(123, stored.Time);
        Assert.Equal(456, stored.Memory);
    }

    [Fact]
    public async Task SolutionService_UpdatesRemoteVerdictEndToEnd()
    {
        using var seed = new JudgeSeed();
        seed.User("ana", nick: "Ana");
        var p = seed.Problem("A");
        var remote = seed.Solution("ana", p, 0);
        var local = seed.Solution("ana", p, 0);
        var row = seed.Db.Solutions.Single(item => item.SolutionId == remote);
        row.IsRemoteOj = true;
        seed.Save();
        var service = new OnlineJudgeAdmin.Core.Application.Services.Implementations.SolutionService(Repository(seed), Mock.Of<FluentValidation.IValidator<Problem>>());

        await service.UpdateSolutionRemoteAsync(new Solution { SolutionId = remote, Result = 4, Time = 15, Memory = 2048 });

        var stored = await seed.Db.Solutions.AsNoTracking().SingleAsync(item => item.SolutionId == remote);
        Assert.Equal(4, stored.Result);
        Assert.Equal(15, stored.Time);
        Assert.Equal("ana", stored.UserId);
        await Assert.ThrowsAsync<Exception>(() => service.UpdateSolutionRemoteAsync(new Solution { SolutionId = local, Result = 4 }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateSolutionRemoteAsync(new Solution { SolutionId = 999, Result = 4 }));
    }

    [Fact]
    public async Task Audit_FiltersPagesAndMapsIp()
    {
        using var seed = new JudgeSeed();
        seed.User("ana", nick: "Ana").Language(1, "C++");
        var p1 = seed.Problem("A");
        var p2 = seed.Problem("B");
        var target = seed.Solution("ana", p1, 4);
        seed.Solution("ana", p2, 4);
        seed.Solution("bob", p1, 4);
        seed.Solution("ana", p1, 4, siteId: 2);
        var repository = Repository(seed);

        var filtered = await repository.GetSubmissionAuditAsync(1, 1, 50, p1, " ana ", " 10.0 ");
        var all = await repository.GetSubmissionAuditAsync(1, 0, 1000, null, null, null);
        var none = await repository.GetSubmissionAuditAsync(1, 1, 50, null, null, "192.168");

        var item = Assert.Single(filtered.Items);
        Assert.Equal(target, item.SolutionId);
        Assert.Equal("Ana", item.Nick);
        Assert.Equal("C++", item.LanguageName);
        Assert.Equal("10.0.0.1", item.ClientIp);
        Assert.Equal(1, all.Page);
        Assert.Equal(200, all.PageSize);
        Assert.Equal(3, all.Total);
        Assert.Empty(none.Items);
    }
}

public class JudgeRepositoryTests
{
    private static JudgeRepository Repository(JudgeSeed seed) => new(seed.Db, CreateMapper());

    private static async Task<short> Result(JudgeSeed seed, int solutionId) =>
        (await seed.Db.Solutions.AsNoTracking().SingleAsync(item => item.SolutionId == solutionId)).Result;

    [Fact]
    public async Task RejudgeOperations_OnlyTouchMatchingSiteRows()
    {
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("A");
        var p2 = seed.Problem("B");
        var contest = seed.Contest("C", DateTime.Now, DateTime.Now.AddHours(1));
        var byId = seed.Solution("ana", p1, 4);
        var byProblem = seed.Solution("ana", p2, 4);
        var byContest = seed.Solution("ana", p1, 6, contestId: contest);
        var byLanguage = seed.Solution("ana", p1, 6, language: 7);
        var otherSite = seed.Solution("ana", p2, 4, siteId: 2, language: 7);
        var repository = Repository(seed);

        Assert.Equal(1, await repository.RejudgeSolutionByIdAsync(1, byId));
        Assert.Equal(1, await repository.RejudgeSolutionByProblemIdAsync(1, p2));
        Assert.Equal(1, await repository.RejudgeSolutionByContestIdAsync(1, contest));
        Assert.Equal(1, await repository.RejudgeSolutionsByLanguageAsync(1, 7));
        Assert.Equal(0, await repository.RejudgeSolutionByIdAsync(2, byId));

        Assert.Equal(JudgeResultCodes.WaitRejudge, await Result(seed, byId));
        Assert.Equal(JudgeResultCodes.WaitRejudge, await Result(seed, byProblem));
        Assert.Equal(JudgeResultCodes.WaitRejudge, await Result(seed, byContest));
        Assert.Equal(JudgeResultCodes.WaitRejudge, await Result(seed, byLanguage));
        Assert.Equal(JudgeResultCodes.Accepted, await Result(seed, otherSite));
    }

    [Fact]
    public async Task RejudgeRange_IsInclusive()
    {
        using var seed = new JudgeSeed();
        var p = seed.Problem("A");
        var ids = Enumerable.Range(0, 4).Select(_ => seed.Solution("ana", p, 4)).ToList();

        Assert.Equal(2, await Repository(seed).RejudgeSolutionsByRangeAsync(1, ids[1], ids[2]));
        Assert.Equal(JudgeResultCodes.Accepted, await Result(seed, ids[0]));
        Assert.Equal(JudgeResultCodes.WaitRejudge, await Result(seed, ids[2]));
        Assert.Equal(JudgeResultCodes.Accepted, await Result(seed, ids[3]));
    }

    [Fact]
    public async Task ManualJudge_SetsVerdictAndJudgeTime()
    {
        using var seed = new JudgeSeed();
        var p = seed.Problem("A");
        var id = seed.Solution("ana", p, 0);

        Assert.Equal(1, await Repository(seed).ManuallyJudgeSolutionAsync(1, id, JudgeResultCodes.WrongAnswer));
        Assert.Equal(0, await Repository(seed).ManuallyJudgeSolutionAsync(2, id, JudgeResultCodes.Accepted));

        var stored = await seed.Db.Solutions.AsNoTracking().SingleAsync(item => item.SolutionId == id);
        Assert.Equal(JudgeResultCodes.WrongAnswer, stored.Result);
        Assert.NotNull(stored.Judgetime);
    }

    [Fact]
    public async Task RejudgeHistory_ListsPendingRejudgesNewestFirst()
    {
        using var seed = new JudgeSeed();
        var p = seed.Problem("A");
        var older = seed.Solution("ana", p, JudgeResultCodes.WaitRejudge);
        var newer = seed.Solution("bob", p, JudgeResultCodes.WaitRejudge, language: 3);
        seed.Solution("ana", p, 4);
        seed.Solution("ana", p, JudgeResultCodes.WaitRejudge, siteId: 2);

        var history = await Repository(seed).GetRejudgeHistoryAsync(1, 1);

        Assert.Equal(2, history.Total);
        var item = Assert.Single(history.Items);
        Assert.Equal(newer, item.SolutionId);
        Assert.Equal(3, item.LanguageId);
        Assert.NotEqual(older, item.SolutionId);
    }
}
