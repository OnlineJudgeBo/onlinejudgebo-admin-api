using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;

public class PublicRepositoryContestsTests
{
    private static readonly DateTime Now = DateTime.Now;

    private static CurrentUser Student(string userId = "ana", int siteId = 1) => new() { UserId = userId, SiteId = siteId, Role = UserRolesEnum.Invitado };

    private static CurrentUser Staff(UserRolesEnum role = UserRolesEnum.Docente, int siteId = 1) => new() { UserId = "staff", SiteId = siteId, Role = role };

    [Fact]
    public async Task GetContests_ComputesStatusCountsAndDefaults()
    {
        using var seed = new JudgeSeed();
        var running = seed.Contest("Running", Now.AddMinutes(-30), Now.AddMinutes(90), track: "OBI", level: "NATIONAL");
        var p1 = seed.Problem("A");
        var p2 = seed.Problem("B");
        seed.ContestProblem(running, p1, 0).ContestProblem(running, p2, 1);
        seed.ContestUser(running, "ana").ContestUser(running, "bob").ContestUser(running, "zed", siteId: 2);
        seed.Contest("", Now.AddDays(1), Now.AddDays(1).AddSeconds(10), track: "", level: "");

        var result = await seed.PublicRepository().GetContestsAsync(1, null, null, null, 1, 10, null);

        Assert.Equal(2, result.Total);
        Assert.Equal("ALL", result.FilterStatus);
        Assert.Equal("ALL", result.FilterLevel);
        Assert.Equal("DATE_DESC", result.SortBy);
        var upcoming = result.Items.First();
        Assert.Equal("UPCOMING", upcoming.Status);
        Assert.StartsWith("Contest #", upcoming.Title);
        Assert.Equal("GENERAL", upcoming.Track);
        Assert.Equal("PRACTICE", upcoming.Level);
        Assert.Equal(1, upcoming.DurationMinutes);
        var active = result.Items.Last();
        Assert.Equal("ACTIVE", active.Status);
        Assert.True(active.Obi);
        Assert.Equal(120, active.DurationMinutes);
        Assert.Equal(2, active.ProblemCount);
        Assert.Equal(2, active.ParticipantCount);
    }

    [Fact]
    public async Task GetContests_ExcludesDefunctAndOtherSites()
    {
        using var seed = new JudgeSeed();
        seed.Contest("Visible", Now.AddDays(-2), Now.AddDays(-1));
        seed.Contest("Deleted", Now.AddDays(-2), Now.AddDays(-1), defunct: "Y");
        seed.Contest("Other site", Now.AddDays(-2), Now.AddDays(-1), siteId: 2);

        var result = await seed.PublicRepository().GetContestsAsync(1, "all", null, null, 1, 10, null);

        Assert.Equal("Visible", Assert.Single(result.Items).Title);
        Assert.Equal("FINISHED", result.Items.Single().Status);
    }

    [Theory]
    [InlineData("active", "ACTIVE", new[] { "Running" })]
    [InlineData(" UPCOMING ", "UPCOMING", new[] { "Soon" })]
    [InlineData("past", "FINISHED", new[] { "Done" })]
    [InlineData("gym", "GYM", new[] { "Gym" })]
    [InlineData("weird", "ALL", new[] { "Soon", "Running", "Done" })]
    public async Task GetContests_FiltersByStatusAndSeparatesPromotedGym(string status, string normalized, string[] expectedTitles)
    {
        using var seed = new JudgeSeed();
        seed.Contest("Running", Now.AddHours(-1), Now.AddHours(1));
        seed.Contest("Soon", Now.AddDays(1), Now.AddDays(2));
        seed.Contest("Done", Now.AddDays(-3), Now.AddDays(-2));
        seed.Contest("Gym", Now.AddDays(-30), Now.AddDays(-29), defunct: "O");

        var result = await seed.PublicRepository().GetContestsAsync(1, status, null, null, 1, 10, null);

        Assert.Equal(normalized, result.FilterStatus);
        Assert.Equal(expectedTitles, result.Items.Select(item => item.Title));
    }

    [Fact]
    public async Task GetContests_PromotedContestStaysActiveAfterEnd()
    {
        using var seed = new JudgeSeed();
        seed.Contest("Gym", Now.AddDays(-30), Now.AddDays(-29), defunct: "O");

        var item = Assert.Single((await seed.PublicRepository().GetContestsAsync(1, "gym", null, null, 1, 10, null)).Items);

        Assert.Equal("ACTIVE", item.Status);
        Assert.True(item.IsPromoted);
    }

    [Theory]
    [InlineData("regional", "REGIONAL", 1)]
    [InlineData("national", "NATIONAL", 1)]
    [InlineData("practice", "PRACTICE", 1)]
    [InlineData("training", "TRAINING", 0)]
    [InlineData("xyz", "ALL", 3)]
    public async Task GetContests_FiltersByLevel(string level, string normalized, int expected)
    {
        using var seed = new JudgeSeed();
        seed.Contest("R", Now.AddDays(-2), Now.AddDays(-1), level: "REGIONAL");
        seed.Contest("N", Now.AddDays(-2), Now.AddDays(-1), level: "NATIONAL");
        seed.Contest("P", Now.AddDays(-2), Now.AddDays(-1), level: "PRACTICE");

        var result = await seed.PublicRepository().GetContestsAsync(1, null, level, null, 1, 10, null);

        Assert.Equal(normalized, result.FilterLevel);
        Assert.Equal(expected, result.Total);
    }

    [Fact]
    public async Task GetContests_SearchesTitleOrIdAndSortsAscending()
    {
        using var seed = new JudgeSeed();
        var first = seed.Contest("ICPC Bolivia", Now.AddDays(-10), Now.AddDays(-9));
        seed.Contest("Clasificatorio icpc", Now.AddDays(-5), Now.AddDays(-4));
        seed.Contest("Otro", Now.AddDays(-3), Now.AddDays(-2));

        var byTitle = await seed.PublicRepository().GetContestsAsync(1, null, null, "date_asc", 1, 10, "  ICPC ");
        var byId = await seed.PublicRepository().GetContestsAsync(1, null, null, null, 1, 10, first.ToString());

        Assert.Equal("DATE_ASC", byTitle.SortBy);
        Assert.Equal(new[] { "ICPC Bolivia", "Clasificatorio icpc" }, byTitle.Items.Select(item => item.Title));
        Assert.Contains(byId.Items, item => item.ContestId == first);
    }

    [Fact]
    public async Task GetContests_PaginatesAfterFiltering()
    {
        using var seed = new JudgeSeed();
        for (var i = 0; i < 5; i++)
        {
            seed.Contest("C" + i, Now.AddDays(-10 + i), Now.AddDays(-9 + i));
        }

        var page2 = await seed.PublicRepository().GetContestsAsync(1, null, null, null, 2, 2, null);

        Assert.Equal(5, page2.Total);
        Assert.Equal(2, page2.Page);
        Assert.Equal(new[] { "C2", "C1" }, page2.Items.Select(item => item.Title));
    }

    [Fact]
    public async Task ContestReport_RanksBySolvedAcceptedSubmissionsAndExcludesCustomInput()
    {
        using var seed = new JudgeSeed();
        seed.User("ana", nick: "Ana", school: "UMSA").User("bob").User("carl");
        var contest = seed.Contest("Final", Now.AddHours(-2), Now.AddHours(2));
        var p1 = seed.Problem("A");
        var p2 = seed.Problem("B");
        seed.ContestProblem(contest, p1, 0).ContestProblem(contest, p2, 1).ContestUser(contest, "ana").ContestUser(contest, "bob");
        seed.Solution("ana", p1, 4, Now.AddMinutes(-100), contestId: contest);
        seed.Solution("ana", p1, 6, Now.AddMinutes(-90), contestId: contest);
        seed.Solution("bob", p1, 4, Now.AddMinutes(-80), contestId: contest);
        seed.Solution("bob", p2, 4, Now.AddMinutes(-70), contestId: contest);
        seed.Solution("carl", p1, 6, Now.AddMinutes(-60), contestId: contest);
        seed.Solution("carl", p1, 4, Now.AddMinutes(-50), contestId: contest, customInput: true);
        seed.Solution("ana", p2, 4, Now.AddMinutes(-40));

        var report = await seed.PublicRepository().GetContestReportAsync(1, contest);

        Assert.Equal(new[] { "bob", "ana", "carl" }, report.Items.Select(item => item.UserId));
        Assert.Equal(new[] { 1, 2, 3 }, report.Items.Select(item => item.Rank));
        var ana = report.Items.ElementAt(1);
        Assert.Equal("Ana", ana.Nick);
        Assert.Equal("UMSA", ana.School);
        Assert.Equal(50m, ana.Accuracy);
        Assert.Equal(2, report.ProblemCount);
        Assert.Equal(2, report.ParticipantCount);
        Assert.Equal(5, report.TotalSubmissions);
        Assert.Equal(3, report.TotalAccepted);
        Assert.Equal("ACTIVE", report.Status);
        Assert.False(report.Items.Any(item => item.IsVirtualParticipant));
    }

    [Fact]
    public async Task ContestReport_MarksVirtualParticipantsOnlyInPromotedContests()
    {
        using var seed = new JudgeSeed();
        var gym = seed.Contest("Gym", Now.AddDays(-10), Now.AddDays(-9), defunct: "O");
        var p1 = seed.Problem("A");
        seed.ContestUser(gym, "ana");
        seed.Solution("ana", p1, 4, contestId: gym);
        seed.Solution("bob", p1, 4, contestId: gym);

        var report = await seed.PublicRepository().GetContestReportAsync(1, gym);

        Assert.True(report.IsPromoted);
        Assert.Equal("ACTIVE", report.Status);
        Assert.True(report.Items.Single(item => item.UserId == "ana").IsVirtualParticipant);
        Assert.False(report.Items.Single(item => item.UserId == "bob").IsVirtualParticipant);
    }

    [Fact]
    public async Task ContestReport_OwnerAndCsvFlags()
    {
        using var seed = new JudgeSeed();
        var contest = seed.Contest("Final", Now.AddDays(-2), Now.AddDays(-1));
        seed.ContestUser(contest, "staff", isOwner: true);
        var repository = seed.PublicRepository();

        var anonymous = await repository.GetContestReportAsync(1, contest);
        var student = await repository.GetContestReportAsync(1, contest, Student());
        var owner = await repository.GetContestReportAsync(1, contest, Staff());

        Assert.False(anonymous.IsOwner);
        Assert.False(anonymous.CanDownloadCsv);
        Assert.False(student.CanDownloadCsv);
        Assert.True(owner.IsOwner);
        Assert.True(owner.CanDownloadCsv);
        Assert.Equal("FINISHED", owner.Status);
    }

    [Fact]
    public async Task ContestReport_UnknownDefunctOrOtherSiteContestIsNotFound()
    {
        using var seed = new JudgeSeed();
        var deleted = seed.Contest("Deleted", Now.AddDays(-2), Now.AddDays(-1), defunct: "Y");
        var otherSite = seed.Contest("Other", Now.AddDays(-2), Now.AddDays(-1), siteId: 2);
        var repository = seed.PublicRepository();

        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetContestReportAsync(1, 999));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetContestReportAsync(1, deleted));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetContestReportAsync(1, otherSite));
    }

    [Fact]
    public async Task PrivateContest_AccessRules()
    {
        using var seed = new JudgeSeed();
        var contest = seed.Contest("Private", Now.AddDays(-1), Now.AddDays(1), isPrivate: true);
        seed.ContestUser(contest, "member");
        var repository = seed.PublicRepository();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.GetContestReportAsync(1, contest));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.GetContestReportAsync(1, contest, Student("outsider")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.GetContestReportAsync(1, contest, Staff(siteId: 2)));

        Assert.True((await repository.GetContestReportAsync(1, contest, Student("member"))).IsPrivate);
        Assert.NotNull(await repository.GetContestReportAsync(1, contest, Staff(UserRolesEnum.Auxiliar)));
    }

    [Fact]
    public async Task CanDownloadCsv_OnlyStaffOfSameSite()
    {
        using var seed = new JudgeSeed();
        var contest = seed.Contest("Final", Now.AddDays(-2), Now.AddDays(-1));
        var repository = seed.PublicRepository();

        Assert.True(await repository.CanDownloadContestReportCsvAsync(Staff(UserRolesEnum.Administrador), 1, contest));
        Assert.True(await repository.CanDownloadContestReportCsvAsync(Staff(UserRolesEnum.Auxiliar), 1, contest));
        Assert.False(await repository.CanDownloadContestReportCsvAsync(Student(), 1, contest));
        Assert.False(await repository.CanDownloadContestReportCsvAsync(Staff(siteId: 2), 1, contest));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.CanDownloadContestReportCsvAsync(Staff(), 1, 999));
    }

    [Fact]
    public async Task RegisterForContest_AddsParticipantOnceAndChecksPrivateAccess()
    {
        using var seed = new JudgeSeed();
        var open = seed.Contest("Open", Now, Now.AddDays(1));
        var hidden = seed.Contest("Hidden", Now, Now.AddDays(1), isPrivate: true);
        var repository = seed.PublicRepository();

        await repository.RegisterForContestAsync(Student(), 1, open);
        await repository.RegisterForContestAsync(Student(), 1, open);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.RegisterForContestAsync(Student(), 1, hidden));
        await repository.RegisterForContestAsync(Staff(), 1, hidden);

        var registrations = await seed.Db.ContestUsers.AsNoTracking().ToListAsync();
        Assert.Equal(2, registrations.Count);
        Assert.All(registrations, item => Assert.False(item.IsOwner));
    }
}
