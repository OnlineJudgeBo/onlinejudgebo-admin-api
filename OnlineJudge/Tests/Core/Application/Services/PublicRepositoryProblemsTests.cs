using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

public class PublicRepositoryProblemsTests
{
    private static readonly DateTime Now = DateTime.Now;

    private static CurrentUser Student(string userId = "ana") => new() { UserId = userId, SiteId = 1, Role = UserRolesEnum.Invitado };

    private static void SetStats(JudgeSeed seed, int problemId, int accepted, int submit)
    {
        var problem = seed.Db.Problems.Single(item => item.ProblemId == problemId);
        problem.Accepted = accepted;
        problem.Submit = submit;
        seed.Save();
    }

    private static Task<PublicProblemsResponse> List(JudgeSeed seed, string? search = null, int? year = null, string? track = null, string? source = null, string? tag = null, string? sort = null, int? contestId = null, CurrentUser? user = null, int page = 1, int pageSize = 50)
        => seed.PublicRepository().GetProblemsAsync(1, page, pageSize, search, year, track, source, tag, sort, contestId, user);

    [Fact]
    public async Task GetProblems_ListsActiveSiteProblemsNewestFirst()
    {
        using var seed = new JudgeSeed();
        var a = seed.Problem("A");
        var b = seed.Problem("B");
        seed.Problem("Inactive", active: false);
        seed.Problem("Other site", siteId: 2);
        seed.Problem("Deleted", defunct: "D");

        var result = await List(seed);

        Assert.Equal(new[] { b, a }, result.Items.Select(item => item.ProblemId));
        Assert.Equal(2, result.Total);
    }

    [Fact]
    public async Task GetProblems_CurrentlyIncludesProblemsHiddenByAdmin()
    {
        // Documents current behavior: Defunct "Y" (hidden via ChangeProblemVisibility) still shows in the public list.
        using var seed = new JudgeSeed();
        var hidden = seed.Problem("Hidden", defunct: "Y");

        Assert.Equal(hidden, Assert.Single((await List(seed)).Items).ProblemId);
    }

    [Fact]
    public async Task GetProblems_SearchesIdTitleAndDescription()
    {
        using var seed = new JudgeSeed();
        var suma = seed.Problem("Suma de enteros");
        var grafo = seed.Problem("Caminos");
        seed.Problem("Otro");

        Assert.Equal(suma, Assert.Single((await List(seed, search: " SUMA ")).Items).ProblemId);
        Assert.Equal(grafo, Assert.Single((await List(seed, search: "caminos desc")).Items).ProblemId);
        Assert.Contains((await List(seed, search: suma.ToString())).Items, item => item.ProblemId == suma);
    }

    [Fact]
    public async Task GetProblems_FiltersByTagSourceTrackAndYear()
    {
        using var seed = new JudgeSeed();
        var obi = seed.Problem("OBI 2024", originSource: "OBI  2024   Final", inDate: new DateTime(2020, 5, 1));
        var general = seed.Problem("General", inDate: new DateTime(2019, 1, 1));
        var contest = seed.Contest("OBI Final", new DateTime(2024, 6, 1), new DateTime(2024, 6, 2), track: "OBI");
        seed.ContestProblem(contest, obi, 0);
        seed.Tag("Grafos", obi);

        Assert.Equal(obi, Assert.Single((await List(seed, tag: "grafos")).Items).ProblemId);
        Assert.Equal(obi, Assert.Single((await List(seed, source: "obi 2024 final")).Items).ProblemId);
        Assert.Equal(obi, Assert.Single((await List(seed, track: "obi")).Items).ProblemId);
        Assert.Equal(general, Assert.Single((await List(seed, track: "general")).Items).ProblemId);
        Assert.Equal(obi, Assert.Single((await List(seed, year: 2024)).Items).ProblemId);
        Assert.Equal(general, Assert.Single((await List(seed, year: 2019)).Items).ProblemId);
        Assert.Equal(2, (await List(seed, source: "all")).Total);
        Assert.Equal(2, (await List(seed, track: "unknown")).Total);
    }

    [Fact]
    public async Task GetProblems_ItemCarriesMetadataDifficultyAndUserStatus()
    {
        using var seed = new JudgeSeed();
        var easy = seed.Problem("Easy", originSource: "ICPC Bolivia 2023");
        var hard = seed.Problem("Hard");
        SetStats(seed, easy, 70, 100);
        SetStats(seed, hard, 1, 100);
        seed.Tag("A", easy).Tag("B", easy).Tag("C", easy).Tag("D", easy);
        seed.Solution("ana", easy, 4);
        seed.Solution("ana", hard, 6);

        var items = (await List(seed, user: Student())).Items.ToDictionary(item => item.ProblemId);

        Assert.Equal("EASY", items[easy].Difficulty);
        Assert.Equal(70m, items[easy].SuccessRate);
        Assert.Equal(3, items[easy].Tags.Count);
        Assert.Equal("ICPC Bolivia 2023", items[easy].OriginSource);
        Assert.True(items[easy].IsSolvedByCurrentUser);
        Assert.Equal("HARD", items[hard].Difficulty);
        Assert.False(items[hard].IsSolvedByCurrentUser);
        Assert.True(items[hard].IsAttemptedByCurrentUser);
        Assert.Equal("General", items[hard].OriginSource);
        Assert.Equal(new[] { "GENERAL" }, items[hard].ContestTracks);
    }

    [Fact]
    public async Task GetProblems_AnonymousHasNoUserStatus()
    {
        using var seed = new JudgeSeed();
        var p = seed.Problem("A");
        seed.Solution("ana", p, 4);

        var item = Assert.Single((await List(seed)).Items);

        Assert.False(item.IsSolvedByCurrentUser);
        Assert.False(item.IsAttemptedByCurrentUser);
    }

    [Theory]
    [InlineData("id_asc", new[] { 0, 1, 2 })]
    [InlineData("id_desc", new[] { 2, 1, 0 })]
    [InlineData("accepted_asc", new[] { 1, 2, 0 })]
    [InlineData("accepted_desc", new[] { 0, 2, 1 })]
    [InlineData("submit_asc", new[] { 2, 0, 1 })]
    [InlineData("submit_desc", new[] { 1, 0, 2 })]
    [InlineData("success_rate_asc", new[] { 1, 0, 2 })]
    [InlineData("success_rate_desc", new[] { 2, 0, 1 })]
    [InlineData("status_desc", new[] { 1, 2, 0 })]
    [InlineData("status_asc", new[] { 0, 2, 1 })]
    [InlineData("nonsense", new[] { 2, 1, 0 })]
    public async Task GetProblems_Sorts(string sort, int[] expectedOrder)
    {
        using var seed = new JudgeSeed();
        var ids = new[] { seed.Problem("P0"), seed.Problem("P1"), seed.Problem("P2") };
        SetStats(seed, ids[0], 30, 50);
        SetStats(seed, ids[1], 5, 90);
        SetStats(seed, ids[2], 10, 10);
        seed.Solution("ana", ids[1], 4);
        seed.Solution("ana", ids[2], 6);

        var result = await List(seed, sort: sort, user: Student());

        Assert.Equal(expectedOrder.Select(index => ids[index]), result.Items.Select(item => item.ProblemId));
    }

    [Fact]
    public async Task GetProblems_Paginates()
    {
        using var seed = new JudgeSeed();
        var ids = Enumerable.Range(0, 5).Select(index => seed.Problem("P" + index)).ToList();

        var page = await List(seed, sort: "id_asc", page: 2, pageSize: 2);

        Assert.Equal(5, page.Total);
        Assert.Equal(new[] { ids[2], ids[3] }, page.Items.Select(item => item.ProblemId));
    }

    [Fact]
    public async Task GetProblems_ContestScopeUsesLettersOrderAndContestStats()
    {
        using var seed = new JudgeSeed();
        var a = seed.Problem("A");
        var b = seed.Problem("B");
        seed.Problem("Not in contest");
        SetStats(seed, a, 999, 999);
        var contest = seed.Contest("Final", Now.AddHours(-1), Now.AddHours(1));
        seed.ContestProblem(contest, b, 0).ContestProblem(contest, a, 1);
        seed.Solution("ana", a, 4, contestId: contest, num: 1);
        seed.Solution("bob", a, 6, contestId: contest, num: 1);
        seed.Solution("ana", a, 4);

        var result = await List(seed, contestId: contest, user: Student());

        Assert.Equal(new[] { b, a }, result.Items.Select(item => item.ProblemId));
        Assert.Equal(new[] { "A", "B" }, result.Items.Select(item => item.ContestProblemId));
        var itemA = result.Items.Last();
        Assert.Equal(1, itemA.Accepted);
        Assert.Equal(2, itemA.Submit);
        Assert.Equal(contest, itemA.ContestId);
        Assert.True(itemA.IsSolvedByCurrentUser);
        Assert.Equal(0, result.Items.First().Submit);
        Assert.Equal(b, Assert.Single((await List(seed, search: "a", contestId: contest)).Items.Where(item => item.ContestProblemId == "A")).ProblemId);
    }

    [Fact]
    public async Task GetProblems_PrivateContestScopeRequiresAccess()
    {
        using var seed = new JudgeSeed();
        var hidden = seed.Contest("Hidden", Now.AddHours(-1), Now.AddHours(1), isPrivate: true);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => List(seed, contestId: hidden));
        await Assert.ThrowsAsync<ArgumentException>(() => List(seed, contestId: 999));
    }

    [Fact]
    public async Task GetProblemDetail_MapsFieldsAndOrderedSamples()
    {
        using var seed = new JudgeSeed();
        var p = seed.Problem("Suma", source: "Autor", inDate: new DateTime(2022, 3, 4));
        var problem = seed.Db.Problems.Single(item => item.ProblemId == p);
        problem.Input = "in";
        problem.Output = "out";
        problem.Hint = "hint";
        problem.TimeLimit = 2;
        problem.MemoryLimit = 0;
        seed.Db.ProblemSampleCases.AddRange(
            new DbProblemSample { ProblemId = p, Num = 2, Input = "3 4", Output = "7" },
            new DbProblemSample { ProblemId = p, Num = 1, Input = "1 2", Output = "3" });
        seed.Save();

        var detail = await seed.PublicRepository().GetProblemDetailAsync(1, p);

        Assert.Equal("Suma", detail.Title);
        Assert.Equal("in", detail.InputSpec);
        Assert.Equal("hint", detail.Hint);
        Assert.Equal("Autor", detail.Author);
        Assert.Equal(2m, detail.TimeLimitSeconds);
        Assert.Equal(1, detail.MemoryLimitMb);
        Assert.Equal(new[] { 2022 }, detail.Years);
        Assert.Equal(new[] { 1, 2 }, detail.SampleCases.Select(sample => sample.Index));
        Assert.Equal("3", detail.SampleCases.First().Output);
    }

    [Fact]
    public async Task GetProblemDetail_FallsBackToLegacySamplePair()
    {
        using var seed = new JudgeSeed();
        var p = seed.Problem("Legacy");
        var problem = seed.Db.Problems.Single(item => item.ProblemId == p);
        problem.SampleInput = "5";
        seed.Save();

        var sample = Assert.Single((await seed.PublicRepository().GetProblemDetailAsync(1, p)).SampleCases);

        Assert.Equal(1, sample.Index);
        Assert.Equal("5", sample.Input);
        Assert.Equal(string.Empty, sample.Output);
    }

    [Fact]
    public async Task GetProblemDetail_RejectsUnavailableProblems()
    {
        using var seed = new JudgeSeed();
        var inactive = seed.Problem("A", active: false);
        var otherSite = seed.Problem("B", siteId: 2);
        var deleted = seed.Problem("C", defunct: "D");
        var repository = seed.PublicRepository();

        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetProblemDetailAsync(1, inactive));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetProblemDetailAsync(1, otherSite));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetProblemDetailAsync(1, deleted));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetProblemDetailAsync(1, 999));
    }

    [Fact]
    public async Task GetContestProblemDetail_ResolvesLetterAndChecksAccess()
    {
        using var seed = new JudgeSeed();
        var p = seed.Problem("Contest problem", defunct: "Y");
        var open = seed.Contest("Open", Now.AddHours(-1), Now.AddHours(1));
        var hidden = seed.Contest("Hidden", Now.AddHours(-1), Now.AddHours(1), isPrivate: true);
        seed.ContestProblem(open, p, 2).ContestProblem(hidden, p, 0);
        var repository = seed.PublicRepository();

        var detail = await repository.GetContestProblemDetailAsync(1, open, "c");

        Assert.Equal(p, detail.ProblemId);
        Assert.Equal(open, detail.ContestId);
        Assert.Equal("C", detail.ContestProblemId);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetContestProblemDetailAsync(1, open, "Z"));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetContestProblemDetailAsync(1, open, "??"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.GetContestProblemDetailAsync(1, hidden, "A"));
    }

    [Fact]
    public async Task GetProblemStatistics_AggregatesVerdictsLanguagesAndBestRuns()
    {
        using var seed = new JudgeSeed();
        seed.User("ana", nick: "Ana").Language(1, "C++").Language(2, "Python");
        var p = seed.Problem("Suma");
        var first = seed.Solution("ana", p, 4, Now.AddMinutes(-50), language: 1, time: 30, memory: 900);
        var fastest = seed.Solution("bob", p, 4, Now.AddMinutes(-40), language: 2, time: 10, memory: 5000);
        seed.Solution("carl", p, 4, Now.AddMinutes(-30), language: 1, time: 0, memory: 100);
        seed.Solution("ana", p, 6, language: 1);
        seed.Solution("ana", p, 7, language: 1);
        seed.Solution("ana", p, 8, language: 2);
        seed.Solution("ana", p, 10, language: 2);
        seed.Solution("ana", p, 11, language: 1);
        seed.Solution("ana", p, 5, language: 1);
        seed.Solution("ana", p, 4, customInput: true);
        seed.Solution("ana", p, 4, siteId: 2);

        var stats = await seed.PublicRepository().GetProblemStatisticsAsync(1, p);

        Assert.Equal(9, stats.TotalSubmissions);
        Assert.Equal(3, stats.Accepted);
        Assert.Equal(1, stats.WrongAnswer);
        Assert.Equal(1, stats.TimeLimitExceeded);
        Assert.Equal(1, stats.MemoryLimitExceeded);
        Assert.Equal(1, stats.RuntimeError);
        Assert.Equal(1, stats.CompileError);
        Assert.Equal(1, stats.OtherResults);
        Assert.Equal(33.33m, stats.AcceptanceRate);
        Assert.Equal("C++", stats.Languages.First().LanguageName);
        Assert.Equal(6, stats.Languages.First().Submissions);
        Assert.Equal(fastest, stats.BestTime!.SolutionId);
        Assert.Equal("Python", stats.BestTime.LanguageName);
        Assert.Equal(100, stats.BestMemory!.MemoryKb);
        Assert.Equal(first, stats.FirstAccepted.First().SolutionId);
        Assert.Equal("Ana", stats.FirstAccepted.First().Nick);
    }

    [Fact]
    public async Task GetProblemStatistics_EmptyAndMissingProblem()
    {
        using var seed = new JudgeSeed();
        var p = seed.Problem("");
        var repository = seed.PublicRepository();

        var stats = await repository.GetProblemStatisticsAsync(1, p);

        Assert.Equal($"Problema #{p}", stats.Title);
        Assert.Equal(0, stats.AcceptanceRate);
        Assert.Null(stats.BestTime);
        Assert.Null(stats.BestMemory);
        Assert.Empty(stats.FirstAccepted);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetProblemStatisticsAsync(1, 999));
    }

    [Fact]
    public async Task GetProblemFilters_BuildsYearsTracksTagsAndMenu()
    {
        using var seed = new JudgeSeed();
        var obi = seed.Problem("A", originSource: "OBI", inDate: new DateTime(2021, 1, 1));
        var regional = seed.Problem("B", originSource: "Regional  La Paz", inDate: new DateTime(2020, 1, 1));
        seed.Problem("C", originSource: "General 2019", inDate: new DateTime(2019, 1, 1));
        var contest = seed.Contest("ICPC", new DateTime(2023, 9, 1), new DateTime(2023, 9, 2), track: "ICPC_BOLIVIA");
        seed.ContestProblem(contest, regional, 0);
        seed.Tag("Grafos", obi).Tag("dp", regional);

        var filters = await seed.PublicRepository().GetProblemFiltersAsync(1);

        Assert.Equal(new[] { 2023, 2021, 2020, 2019 }, filters.Years);
        Assert.Equal(new[] { "GENERAL", "ICPC_BOLIVIA" }, filters.ContestTracks);
        Assert.Equal(new[] { "dp", "Grafos" }, filters.Tags);
        Assert.Equal(new[] { "Regional La Paz" }, filters.ContestSources);
        Assert.Equal(new[] { "general", "icpc_bolivia", "source:Regional La Paz" }, filters.ProblemMenuItems.Select(item => item.Key));
    }

    [Fact]
    public async Task GetTopics_GroupsClassificationsWithSiteProblemCounts()
    {
        using var seed = new JudgeSeed();
        var a = seed.Problem("A");
        var b = seed.Problem("B");
        var other = seed.Problem("C", siteId: 2);
        seed.Tag("Grafos", a, b).Tag("DP", a).Tag("Strings", other);

        var topics = await seed.PublicRepository().GetTopicsAsync(1);

        var topic = Assert.Single(topics.Topics);
        Assert.Equal(3, topic.ProblemCount);
        Assert.Equal(new[] { "Grafos", "DP" }, topic.Classifications.Select(item => item.Name));
        Assert.Empty(topics.LearningPath);
        Assert.Equal(4, topics.TransversalSkills.Count);
    }
}
