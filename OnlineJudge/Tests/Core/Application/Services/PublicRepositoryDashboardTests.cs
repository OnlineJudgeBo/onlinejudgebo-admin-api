public class PublicRepositoryDashboardTests
{
    [Fact]
    public async Task Dashboard_CountsOnlySiteDataAndExcludesCustomInputRuns()
    {
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("A");
        seed.Problem("B");
        seed.Problem("Inactive", active: false);
        seed.Problem("Other site", siteId: 2);
        seed.Solution("ana", p1, 4, DateTime.Now.AddDays(-1));
        seed.Solution("ana", p1, 6, DateTime.Now.AddDays(-2));
        seed.Solution("bob", p1, 4, DateTime.Now.AddDays(-3));
        seed.Solution("old", p1, 4, DateTime.Now.AddDays(-45));
        seed.Solution("carl", p1, 4, DateTime.Now, customInput: true);
        seed.Solution("dan", p1, 4, DateTime.Now, siteId: 2);

        var dashboard = await seed.PublicRepository().GetDashboardAsync(1);

        Assert.Equal(1, dashboard.SiteId);
        Assert.Equal(2, dashboard.Metrics.Problems);
        Assert.Equal(2, dashboard.Metrics.ActiveUsers);
        Assert.Equal(3, dashboard.Metrics.SubmissionsLast30Days);
    }

    [Fact]
    public async Task Dashboard_ListsActiveAndUpcomingContestsOfTheSite()
    {
        using var seed = new JudgeSeed();
        var now = DateTime.Now;
        seed.Contest("Running", now.AddHours(-1), now.AddHours(1));
        seed.Contest("Finished", now.AddDays(-2), now.AddDays(-1));
        seed.Contest("Defunct", now.AddHours(-1), now.AddHours(1), defunct: "Y");
        seed.Contest("Other site", now.AddHours(-1), now.AddHours(1), siteId: 2);
        seed.Contest("Later", now.AddDays(3), now.AddDays(4));
        seed.Contest("", now.AddDays(1), now.AddDays(2));
        for (var i = 0; i < 5; i++)
        {
            seed.Contest("Far " + i, now.AddDays(10 + i), now.AddDays(11 + i));
        }

        var dashboard = await seed.PublicRepository().GetDashboardAsync(1);

        Assert.Equal(1, dashboard.Metrics.ActiveContests);
        Assert.Equal(5, dashboard.UpcomingContests.Count);
        Assert.StartsWith("Contest #", dashboard.UpcomingContests.First().Title);
        Assert.Equal("Later", dashboard.UpcomingContests.ElementAt(1).Title);
    }

    [Fact]
    public async Task Dashboard_TrendingTopicsCountOnlyActiveSiteProblems()
    {
        using var seed = new JudgeSeed();
        var a = seed.Problem("A");
        var b = seed.Problem("B");
        var inactive = seed.Problem("C", active: false);
        var otherSite = seed.Problem("D", siteId: 2);
        seed.Tag("Grafos", a, b).Tag("DP", a).Tag("Strings", inactive, otherSite);

        var dashboard = await seed.PublicRepository().GetDashboardAsync(1);

        Assert.Equal(new[] { "Grafos", "DP" }, dashboard.TrendingTopics);
    }

    [Fact]
    public async Task Ranking_OrdersBySolvedThenFewerSubmissionsThenUser()
    {
        using var seed = new JudgeSeed();
        seed.User("ana", nick: "Ana", school: "UMSA").User("bob", nick: "").User("carl");
        var p1 = seed.Problem("A");
        var p2 = seed.Problem("B");
        seed.Solution("ana", p1, 4);
        seed.Solution("ana", p1, 4);
        seed.Solution("ana", p2, 4);
        seed.Solution("bob", p1, 4);
        seed.Solution("bob", p2, 4);
        seed.Solution("carl", p1, 6);
        seed.Solution("carl", p1, 4, customInput: true);
        seed.Solution("zed", p1, 4, siteId: 2);

        var ranking = await seed.PublicRepository().GetRankingAsync(1, 10, null);

        Assert.Equal(new[] { "bob", "ana", "carl" }, ranking.Items.Select(item => item.UserId));
        var bob = ranking.Items.First();
        Assert.Equal(1, bob.Rank);
        Assert.Equal(2, bob.Solved);
        Assert.Equal(100m, bob.Ratio);
        Assert.Equal("bob", bob.Nick);
        var ana = ranking.Items.ElementAt(1);
        Assert.Equal("Ana", ana.Nick);
        Assert.Equal("UMSA", ana.School);
        Assert.Equal(66.67m, ana.Ratio);
        Assert.Equal(0, ranking.Items.Last().Solved);
    }

    [Fact]
    public async Task Ranking_RespectsLimitAndUsesUserIdWhenProfileMissing()
    {
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("A");
        seed.Solution("ghost", p1, 4);
        seed.Solution("second", p1, 6);

        var ranking = await seed.PublicRepository().GetRankingAsync(1, 1, null);

        var only = Assert.Single(ranking.Items);
        Assert.Equal("ghost", only.Nick);
        Assert.Equal(string.Empty, only.School);
    }

    [Theory]
    [InlineData("d", 1)]
    [InlineData(" D ", 1)]
    [InlineData("w", 2)]
    [InlineData("all", 4)]
    [InlineData(null, 4)]
    [InlineData("unknown", 4)]
    public async Task Ranking_ScopeFiltersByDate(string? scope, int expectedUsers)
    {
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("A");
        var today = DateTime.Now.Date;
        seed.Solution("today", p1, 4, today.AddHours(1));
        seed.Solution("thisweek", p1, 4, today.AddDays(-5));
        seed.Solution("lastmonth", p1, 4, today.AddDays(-40));
        seed.Solution("ancient", p1, 4, new DateTime(2015, 1, 1));

        var ranking = await seed.PublicRepository().GetRankingAsync(1, 50, scope);

        Assert.Equal(expectedUsers, ranking.Items.Count);
    }

    [Fact]
    public async Task Ranking_MonthScopeStartsOnFirstDay()
    {
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("A");
        var firstOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        seed.Solution("inmonth", p1, 4, firstOfMonth.AddMinutes(1));
        seed.Solution("before", p1, 4, firstOfMonth.AddMinutes(-1));

        var ranking = await seed.PublicRepository().GetRankingAsync(1, 50, "m");

        Assert.Equal("inmonth", Assert.Single(ranking.Items).UserId);
    }

    [Fact]
    public async Task Languages_SkipsBlankNamesAndOrdersById()
    {
        using var seed = new JudgeSeed();
        seed.Language(3, "Python").Language(1, "C++").Language(2, " ");

        var languages = await seed.PublicRepository().GetLanguagesAsync();

        Assert.Equal(new[] { 1, 3 }, languages.Select(item => item.LanguageId));
        Assert.Equal("C++", languages.First().Name);
    }

    [Fact]
    public async Task OnlineUsers_ReturnsOnlyRecentActivityNewestFirst()
    {
        using var seed = new JudgeSeed();
        var now = DateTimeOffset.Now;
        seed.Online("old", now.AddMinutes(-30))
            .Online("recent", now.AddMinutes(-5), now.AddHours(-1), "/oj/problem.php")
            .Online("newest", now.AddMinutes(-1));

        var online = await seed.PublicRepository().GetOnlineUsersAsync(1, 10);

        Assert.Equal(10, online.WindowMinutes);
        Assert.Equal(new[] { "newest", "recent" }, online.Items.Select(item => item.Hash));
        var recent = online.Items.Last();
        Assert.Equal("/oj/problem.php", recent.Uri);
        Assert.NotNull(recent.FirstSeenUtc);
        Assert.Null(online.Items.First().FirstSeenUtc);
    }
}
