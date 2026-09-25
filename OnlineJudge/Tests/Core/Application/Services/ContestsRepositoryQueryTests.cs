using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using static RepositoryTestSupport;

public class ContestsRepositoryQueryTests
{
    private static readonly DateTime Now = DateTime.Now;

    private static ContestsRepository Repository(JudgeSeed seed) => new(seed.Db, CreateMapper());

    [Fact]
    public async Task Docente_SeesOwnContestsOrAllWithPromotedFilter()
    {
        using var seed = new JudgeSeed();
        var mine = seed.Contest("Mine", Now, Now.AddHours(1));
        var others = seed.Contest("Others", Now, Now.AddHours(1));
        var promoted = seed.Contest("Promoted", Now, Now.AddHours(1), defunct: "O");
        seed.Contest("Other site", Now, Now.AddHours(1), siteId: 2);
        seed.ContestUser(mine, "teacher", isOwner: true).ContestUser(promoted, "teacher");
        var repository = Repository(seed);

        var own = (await repository.GetContestsByUserIdDocenteRoleAsync("teacher", false, 1)).Select(item => item.ContestId);
        var ownWithPromoted = (await repository.GetContestsByUserIdDocenteRoleAsync("teacher", false, 1, includePromoted: true)).Select(item => item.ContestId);
        var all = (await repository.GetContestsByUserIdDocenteRoleAsync("teacher", true, 1)).Select(item => item.ContestId);

        Assert.Equal(new[] { mine }, own);
        Assert.Equal(new[] { promoted, mine }, ownWithPromoted);
        Assert.Equal(new[] { others, mine }, all);
    }

    [Fact]
    public async Task Auxiliar_SeesSiteContestsNewestFirstCappedAt100()
    {
        using var seed = new JudgeSeed();
        for (var i = 0; i < 102; i++)
        {
            seed.Db.Contests.Add(new OnlineJudgeAdmin.Infrastructure.Database.Models.DbContest { Title = "C" + i, StartTime = Now, EndTime = Now, Defunct = "N" });
        }

        seed.Save();
        foreach (var id in seed.Db.Contests.Select(item => item.ContestId).ToList())
        {
            seed.Db.ContestSites.Add(new OnlineJudgeAdmin.Infrastructure.Database.Models.DbContestSite { ContestId = id, SiteId = 1 });
        }

        seed.Save();
        var promoted = seed.Contest("Promoted", Now, Now.AddHours(1), defunct: "O");

        var withoutPromoted = (await Repository(seed).GetContestsByAuxiliarRoleAsync("aux", 1)).ToList();
        var withPromoted = (await Repository(seed).GetContestsByAuxiliarRoleAsync("aux", 1, includePromoted: true)).ToList();

        Assert.Equal(100, withoutPromoted.Count);
        Assert.DoesNotContain(withoutPromoted, item => item.ContestId == promoted);
        Assert.Equal(promoted, withPromoted.First().ContestId);
    }

    [Fact]
    public async Task GetContestById_LoadsProblemsUsersAndLanguagesForSiteOnly()
    {
        using var seed = new JudgeSeed();
        seed.Language(1, "C++");
        var contest = seed.Contest("Final", Now, Now.AddHours(1), isPrivate: true, track: "OBI");
        var otherSite = seed.Contest("Other", Now, Now.AddHours(1), siteId: 2);
        var p1 = seed.Problem("A");
        var p2 = seed.Problem("B");
        seed.ContestProblem(contest, p2, 1).ContestProblem(contest, p1, 0);
        seed.ContestUser(contest, "zed").ContestUser(contest, "ana", isOwner: true);
        seed.Db.Database.ExecuteSqlRaw("INSERT INTO contest_programming_language (contest_id, language_id) VALUES ({0}, 1)", contest);
        var repository = Repository(seed);

        var loaded = await repository.GetContestByIdAsync(contest, 1);

        Assert.Equal("Final", loaded.Title);
        Assert.Equal("OBI", loaded.Track);
        Assert.Equal(new int?[] { p1, p2 }, loaded.ContestProblems.Select(item => item.ProblemId));
        Assert.Equal(new[] { "ana", "zed" }, loaded.ContestUsers.Select(item => item.UserId));
        Assert.True(loaded.ContestUsers.First().IsOwner);
        Assert.Equal("C++", loaded.ProgrammingLanguages.Single().Name);
        Assert.Null(await repository.GetContestByIdAsync(otherSite, 1));
    }

    [Fact]
    public async Task UpdateContest_ReplacesProblemsUsersAndLanguagesButKeepsOwner()
    {
        using var seed = new JudgeSeed();
        seed.Language(1, "C++").Language(2, "Python");
        var contest = seed.Contest("Old", Now, Now.AddHours(1));
        var p1 = seed.Problem("A");
        var p2 = seed.Problem("B");
        seed.ContestProblem(contest, p1, 0).ContestUser(contest, "owner", isOwner: true).ContestUser(contest, "old_member");
        var repository = Repository(seed);
        var update = await repository.GetContestByIdAsync(contest, 1);
        update.Title = "New";
        update.ContestProblems = new List<ContestProblem> { new() { ProblemId = p2, Num = 0 } };
        update.ContestUsers = new List<ContestUser> { new() { UserId = "owner", SiteId = 1 }, new() { UserId = "new_member", SiteId = 1 } };
        update.ProgrammingLanguages = new List<ProgrammingLanguage> { new() { LanguageId = 2 }, new() { LanguageId = 99 } };
        seed.Db.ChangeTracker.Clear();

        var updated = await repository.UpdateContestAsync(contest, update, 1);

        Assert.Equal("New", updated.Title);
        Assert.Equal(new int?[] { p2 }, updated.ContestProblems.Select(item => item.ProblemId));
        Assert.Equal(new[] { "new_member", "owner" }, updated.ContestUsers.Select(item => item.UserId));
        Assert.True(updated.ContestUsers.Single(item => item.UserId == "owner").IsOwner);
        Assert.Equal("Python", updated.ProgrammingLanguages.Single().Name);
    }

    [Fact]
    public async Task UpdateContest_RequiresOwnerOnSite()
    {
        using var seed = new JudgeSeed();
        var contest = seed.Contest("Orphan", Now, Now.AddHours(1));
        seed.ContestUser(contest, "member");
        var repository = Repository(seed);
        var update = await repository.GetContestByIdAsync(contest, 1);
        seed.Db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => repository.UpdateContestAsync(contest, update, 1));
    }
}
