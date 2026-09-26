using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

public class PublicRepositorySubmissionsTests
{
    private static readonly DateTime Now = DateTime.Now;

    private static CurrentUser Student(string userId = "ana", int siteId = 1) => new() { UserId = userId, SiteId = siteId, Role = UserRolesEnum.Invitado };

    [Fact]
    public async Task GetSubmissions_MapsNamesVerdictsAndContestLetters()
    {
        using var seed = new JudgeSeed();
        seed.User("ana", nick: "Ana").User("bob", nick: "").Language(1, "C++");
        var p1 = seed.Problem("Suma");
        var contest = seed.Contest("Final", Now.AddDays(-1), Now.AddDays(1));
        var inContest = seed.Solution("ana", p1, 4, contestId: contest, num: 1);
        var practice = seed.Solution("bob", p1, 6, language: 9);

        var result = await seed.PublicRepository().GetSubmissionsAsync(1, 1, 10, null, null, null, null, null);

        Assert.Equal(2, result.Total);
        var newest = result.Items.First();
        Assert.Equal(practice, newest.SolutionId);
        Assert.Equal("bob", newest.Nick);
        Assert.Equal("Lenguaje #9", newest.LanguageName);
        Assert.Null(newest.ContestProblemId);
        Assert.Equal("wrong_answer", newest.StatusKey);
        var contestRow = result.Items.Last();
        Assert.Equal(inContest, contestRow.SolutionId);
        Assert.Equal("Ana", contestRow.Nick);
        Assert.Equal("Suma", contestRow.ProblemTitle);
        Assert.Equal("C++", contestRow.LanguageName);
        Assert.Equal("B", contestRow.ContestProblemId);
        Assert.Equal("accepted", contestRow.StatusKey);
        Assert.True(contestRow.IsFinal);
    }

    [Fact]
    public async Task GetSubmissions_AppliesEveryFilter()
    {
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("A");
        var p2 = seed.Problem("B");
        var contest = seed.Contest("Final", Now.AddDays(-1), Now.AddDays(1));
        var target = seed.Solution("ana", p1, 4, contestId: contest, num: 0, language: 2);
        seed.Solution("ana", p1, 6, contestId: contest, num: 0, language: 2);
        seed.Solution("ana", p2, 4, contestId: contest, num: 1, language: 2);
        seed.Solution("bob", p1, 4, contestId: contest, num: 0, language: 2);
        seed.Solution("ana", p1, 4, contestId: contest, num: 0, language: 1);
        seed.Solution("ana", p1, 4, language: 2);
        seed.Solution("ana", p1, 4, contestId: contest, num: 0, language: 2, siteId: 2);
        seed.Solution("ana", p1, 4, contestId: contest, num: 0, language: 2, customInput: true);

        var result = await seed.PublicRepository().GetSubmissionsAsync(1, 1, 10, contest, p1, " ana ", 2, "accepted");

        Assert.Equal(target, Assert.Single(result.Items).SolutionId);
    }

    [Theory]
    [InlineData("queued", 2)]
    [InlineData("evaluating", 4)]
    [InlineData("finished", 8)]
    [InlineData("pending", 1)]
    [InlineData("pending_rejudge", 1)]
    [InlineData("compile_error", 1)]
    [InlineData(" TIME_LIMIT_EXCEEDED ", 1)]
    [InlineData("unknown", 14)]
    [InlineData(null, 14)]
    public async Task GetSubmissions_StatusKeyGroups(string? statusKey, int expected)
    {
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("A");
        for (short code = 0; code <= 13; code++)
        {
            seed.Solution("ana", p1, code);
        }

        var result = await seed.PublicRepository().GetSubmissionsAsync(1, 1, 100, null, null, null, null, statusKey);

        Assert.Equal(expected, result.Total);
    }

    [Fact]
    public async Task GetSubmissions_PaginatesNewestFirst()
    {
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("A");
        var ids = Enumerable.Range(0, 5).Select(_ => seed.Solution("ana", p1, 4)).ToList();

        var page2 = await seed.PublicRepository().GetSubmissionsAsync(1, 2, 2, null, null, null, null, null);
        var empty = await seed.PublicRepository().GetSubmissionsAsync(1, 9, 2, null, null, null, null, null);

        Assert.Equal(new[] { ids[2], ids[1] }, page2.Items.Select(item => item.SolutionId));
        Assert.Equal(5, empty.Total);
        Assert.Empty(empty.Items);
    }

    [Fact]
    public async Task GetSubmissions_PrivateContestFilterRequiresAccess()
    {
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("A");
        var hidden = seed.Contest("Hidden", Now.AddDays(-1), Now.AddDays(1), isPrivate: true);
        seed.ContestUser(hidden, "member");
        seed.Solution("member", p1, 4, contestId: hidden, num: 0);
        var repository = seed.PublicRepository();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.GetSubmissionsAsync(1, 1, 10, hidden, null, null, null, null));
        Assert.Single((await repository.GetSubmissionsAsync(1, 1, 10, hidden, null, null, null, null, Student("member"))).Items);
    }

    [Fact]
    public async Task GetSubmissions_WithoutContestFilterAlsoListsPrivateContestActivity()
    {
        // Documents current behavior: the unfiltered public list does not hide private-contest submissions.
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("A");
        var hidden = seed.Contest("Hidden", Now.AddDays(-1), Now.AddDays(1), isPrivate: true);
        seed.Solution("member", p1, 4, contestId: hidden, num: 0);

        var result = await seed.PublicRepository().GetSubmissionsAsync(1, 1, 10, null, null, null, null, null);

        Assert.Equal(hidden, Assert.Single(result.Items).ContestId);
    }

    [Fact]
    public async Task GetOwnSubmissions_OnlyCurrentUserAndSite()
    {
        using var seed = new JudgeSeed();
        seed.User("ana", nick: "Ana");
        var p1 = seed.Problem("A");
        var mine = seed.Solution("ana", p1, 4);
        seed.Solution("bob", p1, 4);
        seed.Solution("ana", p1, 4, siteId: 2);
        seed.Solution("ana", p1, 4, customInput: true);

        var result = await seed.PublicRepository().GetOwnSubmissionsAsync(Student(), 1, 10);

        var item = Assert.Single(result.Items);
        Assert.Equal(mine, item.SolutionId);
        Assert.Equal("Ana", item.Nick);
    }

    [Fact]
    public async Task GetOwnSubmissions_FallsBackToUserIdAndHandlesEmptyPage()
    {
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("");
        seed.Solution("ana", p1, 4);

        var result = await seed.PublicRepository().GetOwnSubmissionsAsync(Student(), 1, 10);
        var empty = await seed.PublicRepository().GetOwnSubmissionsAsync(Student("nobody"), 1, 10);

        Assert.Equal("ana", result.Items.Single().Nick);
        Assert.Equal($"Problema #{p1}", result.Items.Single().ProblemTitle);
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.Total);
    }

    [Fact]
    public async Task GetRecentSubmissions_ByContestAndByCourse()
    {
        using var seed = new JudgeSeed();
        var p1 = seed.Problem("A");
        var contest = seed.Contest("Final", Now.AddDays(-1), Now.AddDays(1));
        var inContest = seed.Solution("ana", p1, 4, contestId: contest, num: 0);
        var first = seed.Solution("bob", p1, 4);
        var second = seed.Solution("carl", p1, 6);
        seed.Solution("dan", p1, 6);
        seed.Academic.CourseSubmissionContexts.AddRange(
            new DbCourseSubmissionContext { SolutionId = first, CourseId = 7, AssignmentId = 1, UserId = "bob", CreatedAt = Now.AddMinutes(-10) },
            new DbCourseSubmissionContext { SolutionId = second, CourseId = 7, AssignmentId = 1, UserId = "carl", CreatedAt = Now.AddMinutes(-1) },
            new DbCourseSubmissionContext { SolutionId = inContest, CourseId = 8, AssignmentId = 1, UserId = "ana", CreatedAt = Now });
        await seed.Academic.SaveChangesAsync();
        var repository = seed.PublicRepository();

        var byContest = await repository.GetRecentSubmissionsAsync(1, 1, 10, contest, null);
        var byCourse = await repository.GetRecentSubmissionsAsync(1, 1, 10, null, 7);
        var all = await repository.GetRecentSubmissionsAsync(1, 1, 10, null, null);

        Assert.Equal(inContest, Assert.Single(byContest.Items).SolutionId);
        Assert.Equal(new[] { second, first }, byCourse.Items.Select(item => item.SolutionId));
        Assert.Equal(2, byCourse.Total);
        Assert.Equal(4, all.Total);
    }

    [Fact]
    public async Task GetRecentSubmissions_EmptyCourse()
    {
        using var seed = new JudgeSeed();

        var result = await seed.PublicRepository().GetRecentSubmissionsAsync(1, 1, 10, null, 99);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task GetOwnSourceCodes_ReturnsOnlyOwnNonEmptySources()
    {
        using var seed = new JudgeSeed();
        seed.Language(1, "C++");
        var p1 = seed.Problem("Suma");
        var withSource = seed.Solution("ana", p1, 4, source: "int main(){}");
        seed.Solution("ana", p1, 6, source: "   ");
        seed.Solution("ana", p1, 6);
        seed.Solution("bob", p1, 4, source: "theirs");
        seed.Solution("ana", p1, 4, source: "custom", customInput: true);

        var items = await seed.PublicRepository().GetOwnSubmissionSourceCodesAsync(Student());

        var item = Assert.Single(items);
        Assert.Equal(withSource, item.SolutionId);
        Assert.Equal("int main(){}", item.SourceCode);
        Assert.Equal("Suma", item.ProblemTitle);
        Assert.Equal("C++", item.LanguageName);
        Assert.Equal("accepted", item.StatusKey);
        Assert.Empty(await seed.PublicRepository().GetOwnSubmissionSourceCodesAsync(Student("nobody")));
    }

    [Fact]
    public async Task Submit_PracticeProblemStoresSolutionAndSource()
    {
        using var seed = new JudgeSeed();
        seed.User("ana").Language(1, "C++");
        var p1 = seed.Problem("A");

        var response = await seed.PublicRepository().SubmitAsync(Student(), new PublicSubmissionRequest { ProblemId = p1, SourceCode = "int main(){}", ClientIp = "1.2.3.4" }, 1);

        var solution = await seed.Db.Solutions.AsNoTracking().SingleAsync(item => item.SolutionId == response.SolutionId);
        Assert.Equal(p1, solution.ProblemId);
        Assert.Null(solution.ContestId);
        Assert.Equal(-1, solution.Num);
        Assert.Equal(0, solution.Result);
        Assert.Equal("1.2.3.4", solution.Ip);
        Assert.Equal(12, solution.CodeLength);
        Assert.Equal("int main(){}", (await seed.Db.SourceCodes.AsNoTracking().SingleAsync()).Source);
    }

    [Theory]
    [InlineData("A", null, 0)]
    [InlineData(" b ", null, 1)]
    [InlineData(null, 1, 1)]
    public async Task Submit_ResolvesContestProblemByLetterOrNumber(string? letter, int? num, int expectedNum)
    {
        using var seed = new JudgeSeed();
        seed.User("ana").Language(1, "C++");
        var p1 = seed.Problem("A");
        var p2 = seed.Problem("B");
        var contest = seed.Contest("Final", Now.AddHours(-1), Now.AddHours(1));
        seed.ContestProblem(contest, p1, 0).ContestProblem(contest, p2, 1);

        var response = await seed.PublicRepository().SubmitAsync(Student(), new PublicSubmissionRequest { ContestId = contest, ContestProblemId = letter, Num = num, SourceCode = "code!" }, 1);

        var solution = await seed.Db.Solutions.AsNoTracking().SingleAsync(item => item.SolutionId == response.SolutionId);
        Assert.Equal(contest, solution.ContestId);
        Assert.Equal(expectedNum, solution.Num);
        Assert.Equal(expectedNum == 0 ? p1 : p2, solution.ProblemId);
    }

    [Fact]
    public async Task Submit_ResolvesContestProblemByProblemId()
    {
        using var seed = new JudgeSeed();
        seed.User("ana").Language(1, "C++");
        var p1 = seed.Problem("A");
        var contest = seed.Contest("Final", Now.AddHours(-1), Now.AddHours(1));
        seed.ContestProblem(contest, p1, 3);

        var response = await seed.PublicRepository().SubmitAsync(Student(), new PublicSubmissionRequest { ContestId = contest, ProblemId = p1, SourceCode = "code!" }, 1);

        Assert.Equal(3, (await seed.Db.Solutions.AsNoTracking().SingleAsync(item => item.SolutionId == response.SolutionId)).Num);
    }

    [Fact]
    public async Task Submit_RejectsProblemThatIsNotPartOfTheContest()
    {
        using var seed = new JudgeSeed();
        seed.User("ana").Language(1, "C++");
        var inContest = seed.Problem("A");
        var outside = seed.Problem("Easy outside problem");
        var contest = seed.Contest("Final", Now.AddHours(-1), Now.AddHours(1));
        seed.ContestProblem(contest, inContest, 0);

        await Assert.ThrowsAsync<ArgumentException>(() => seed.PublicRepository().SubmitAsync(Student(), new PublicSubmissionRequest { ContestId = contest, ProblemId = outside, SourceCode = "code!" }, 1));
        Assert.Empty(await seed.Db.Solutions.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData("Z", null)]
    [InlineData("1A", null)]
    [InlineData(null, 5)]
    public async Task Submit_RejectsUnknownContestProblem(string? letter, int? num)
    {
        using var seed = new JudgeSeed();
        seed.User("ana").Language(1, "C++");
        var p1 = seed.Problem("A");
        var contest = seed.Contest("Final", Now.AddHours(-1), Now.AddHours(1));
        seed.ContestProblem(contest, p1, 0);

        await Assert.ThrowsAsync<ArgumentException>(() => seed.PublicRepository().SubmitAsync(Student(), new PublicSubmissionRequest { ContestId = contest, ContestProblemId = letter, Num = num, SourceCode = "code!" }, 1));
    }

    [Theory]
    [InlineData(1, "Este concurso todavía no acepta envíos.")]
    [InlineData(-1, "Este concurso ya finalizó y no acepta envíos.")]
    public async Task Submit_RejectsContestOutsideItsWindow(int direction, string message)
    {
        using var seed = new JudgeSeed();
        seed.User("ana").Language(1, "C++");
        var p1 = seed.Problem("A");
        var contest = direction > 0
            ? seed.Contest("Future", Now.AddHours(1), Now.AddHours(2))
            : seed.Contest("Past", Now.AddHours(-2), Now.AddHours(-1));
        seed.ContestProblem(contest, p1, 0);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => seed.PublicRepository().SubmitAsync(Student(), new PublicSubmissionRequest { ContestId = contest, ContestProblemId = "A", SourceCode = "code!" }, 1));

        Assert.Equal(message, error.Message);
    }

    [Fact]
    public async Task Submit_PromotedContestAcceptsAfterEnd()
    {
        using var seed = new JudgeSeed();
        seed.User("ana").Language(1, "C++");
        var p1 = seed.Problem("A");
        var gym = seed.Contest("Gym", Now.AddDays(-10), Now.AddDays(-9), defunct: "O");
        seed.ContestProblem(gym, p1, 0);

        var response = await seed.PublicRepository().SubmitAsync(Student(), new PublicSubmissionRequest { ContestId = gym, ContestProblemId = "A", SourceCode = "code!" }, 1);

        Assert.True(response.SolutionId > 0);
    }

    [Fact]
    public async Task Submit_PrivateContestRequiresMembership()
    {
        using var seed = new JudgeSeed();
        seed.User("ana").Language(1, "C++");
        var p1 = seed.Problem("A");
        var hidden = seed.Contest("Hidden", Now.AddHours(-1), Now.AddHours(1), isPrivate: true);
        seed.ContestProblem(hidden, p1, 0);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => seed.PublicRepository().SubmitAsync(Student(), new PublicSubmissionRequest { ContestId = hidden, ContestProblemId = "A", SourceCode = "code!" }, 1));
    }

    [Fact]
    public async Task Submit_RejectsInactiveOrDeletedUserAndMissingProblem()
    {
        using var seed = new JudgeSeed();
        seed.User("ana").User("inactive", active: false).Language(1, "C++");
        var p1 = seed.Problem("A");
        var otherSite = seed.Problem("B", siteId: 2);
        var repository = seed.PublicRepository();

        await Assert.ThrowsAsync<ArgumentException>(() => repository.SubmitAsync(Student("inactive"), new PublicSubmissionRequest { ProblemId = p1, SourceCode = "code!" }, 1));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SubmitAsync(Student("ghost"), new PublicSubmissionRequest { ProblemId = p1, SourceCode = "code!" }, 1));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SubmitAsync(Student(), new PublicSubmissionRequest { ProblemId = otherSite, SourceCode = "code!" }, 1));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SubmitAsync(Student(), new PublicSubmissionRequest { SourceCode = "code!" }, 1));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SubmitAsync(Student(), new PublicSubmissionRequest { ProblemId = p1, SourceCode = "code!" }, 99));
    }
}
