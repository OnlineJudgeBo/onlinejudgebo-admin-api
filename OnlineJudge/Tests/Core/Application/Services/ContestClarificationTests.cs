using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using static RepositoryTestSupport;

// Real repositories over SQLite, so the visibility and time rules are checked end to end.
public class ContestClarificationTests
{
    private static readonly DateTime Now = DateTime.Now;
    private static readonly CurrentUser Ana = new() { UserId = "ana", SiteId = 1, Role = UserRolesEnum.Invitado };
    private static readonly CurrentUser Beto = new() { UserId = "beto", SiteId = 1, Role = UserRolesEnum.Invitado };
    private static readonly CurrentUser Judge = new() { UserId = "judge", SiteId = 1, Role = UserRolesEnum.Auxiliar };

    private static ContestClarificationService Service(JudgeSeed seed) =>
        new(new ContestClarificationRepository(seed.Db), new ContestsRepository(seed.Db, CreateMapper()), seed.PublicRepository());

    private static int RunningContest(JudgeSeed seed, DateTime start, DateTime end, bool isPrivate = false)
    {
        var contest = seed.Contest("Final", start, end, isPrivate: isPrivate);
        seed.ContestProblem(contest, seed.Problem("A"), num: 0);
        return contest;
    }

    [Fact]
    public async Task Ask_IsAllowedBeforeAndDuring_ButNotAfterTheContest()
    {
        using var seed = new JudgeSeed();
        var upcoming = RunningContest(seed, Now.AddHours(1), Now.AddHours(2));
        var running = RunningContest(seed, Now.AddHours(-1), Now.AddHours(1));
        var finished = RunningContest(seed, Now.AddHours(-3), Now.AddHours(-1));
        var service = Service(seed);

        await service.AskAsync(1, upcoming, 0, "¿Se permite Python?", Ana);
        await service.AskAsync(1, running, null, "¿El límite es de 1s?", Ana);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AskAsync(1, finished, null, "¿Y ahora?", Ana));
    }

    [Fact]
    public async Task Ask_RejectsEmptyText_UnknownProblem_AndQuestionsTooClose()
    {
        using var seed = new JudgeSeed();
        var contest = RunningContest(seed, Now.AddHours(-1), Now.AddHours(1));
        var service = Service(seed);

        await Assert.ThrowsAsync<ArgumentException>(() => service.AskAsync(1, contest, null, "   ", Ana));
        await Assert.ThrowsAsync<ArgumentException>(() => service.AskAsync(1, contest, 5, "¿Problema F?", Ana));

        await service.AskAsync(1, contest, null, "Primera", Ana);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AskAsync(1, contest, null, "Segunda", Ana));
    }

    [Fact]
    public async Task PrivateContest_OnlyItsParticipantsCanAsk()
    {
        using var seed = new JudgeSeed();
        var contest = RunningContest(seed, Now.AddHours(-1), Now.AddHours(1), isPrivate: true);
        seed.ContestUser(contest, "ana");
        var service = Service(seed);

        await service.AskAsync(1, contest, null, "Hola", Ana);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AskAsync(1, contest, null, "Hola", Beto));
    }

    [Fact]
    public async Task Participants_SeeTheirOwnQuestions_PublicAnswers_AndAnnouncements()
    {
        using var seed = new JudgeSeed();
        var contest = RunningContest(seed, Now.AddHours(-1), Now.AddHours(1));
        var service = Service(seed);
        var privateQuestion = await service.AskAsync(1, contest, null, "Solo para mí", Ana);
        var publicQuestion = await service.AskAsync(1, contest, 0, "¿N puede ser 0?", Beto);
        await service.AnswerAsync(contest, privateQuestion.Id, "Lee el enunciado", isPublic: false, Judge);
        await service.AnswerAsync(contest, publicQuestion.Id, "No, N ≥ 1", isPublic: true, Judge);
        await service.AnnounceAsync(contest, null, "Se rejuzgó el problema A", Judge);

        var forAna = await service.GetForParticipantAsync(1, contest, Ana);
        var forGuest = await service.GetForParticipantAsync(1, contest, null);

        Assert.Equal(3, forAna.Count);
        Assert.Contains(forAna, item => item.Id == privateQuestion.Id && item.UserId == "ana" && item.Answer == "Lee el enunciado");
        var shared = Assert.Single(forAna, item => item.Id == publicQuestion.Id);
        Assert.Null(shared.UserId);
        Assert.All(forAna, item => Assert.Null(item.AnsweredBy));
        Assert.Equal(2, forGuest.Count);
    }

    [Fact]
    public async Task Staff_SeeEveryQuestion_PendingFirst_AndPendingCountsPerContest()
    {
        using var seed = new JudgeSeed();
        var contest = RunningContest(seed, Now.AddHours(-1), Now.AddHours(1));
        var service = Service(seed);
        var answered = await service.AskAsync(1, contest, null, "Primera", Ana);
        await service.AnswerAsync(contest, answered.Id, "Sí", isPublic: false, Judge);
        var pending = await service.AskAsync(1, contest, null, "Segunda", Beto);

        var all = await service.GetAllAsync(contest, 1);
        var counts = await service.GetPendingCountsAsync(1);

        Assert.Equal(new[] { pending.Id, answered.Id }, all.Select(item => item.Id));
        Assert.Equal("judge", all.Single(item => item.Id == answered.Id).AnsweredBy);
        var count = Assert.Single(counts);
        Assert.Equal((contest, 1), (count.ContestId, count.Pending));
    }

    [Fact]
    public async Task Answer_FromAnotherContest_IsNotFound()
    {
        using var seed = new JudgeSeed();
        var contest = RunningContest(seed, Now.AddHours(-1), Now.AddHours(1));
        var other = RunningContest(seed, Now.AddHours(-1), Now.AddHours(1));
        var service = Service(seed);
        var question = await service.AskAsync(1, contest, null, "Hola", Ana);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AnswerAsync(other, question.Id, "Hola", false, Judge));
    }
}
