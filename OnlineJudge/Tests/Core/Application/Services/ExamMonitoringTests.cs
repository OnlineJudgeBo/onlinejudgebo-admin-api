using Microsoft.Extensions.Configuration;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
using OnlineJudgeAdminApi.Controllers;
using static ControllerTestSupport;
using static RepositoryTestSupport;

public class ExamMonitorServiceTests
{
    private readonly Mock<IContestsRepository> _contests = new();
    private readonly Mock<IUserRepository> _users = new();

    private ContestService Service => new(_contests.Object, Mock.Of<IProblemRepository>(), Mock.Of<IPrivilegeRepository>(), _users.Object, Mock.Of<IValidator<Problem>>());

    private static Contest Contest(bool isExam, string? labIps = null) => new()
    {
        ContestId = 5, Title = "Parcial", StartTime = new DateTime(2026, 9, 25, 8, 0, 0), EndTime = new DateTime(2026, 9, 25, 10, 0, 0),
        IsExam = isExam, ExamLabIps = labIps, Defunct = "N", Track = "GENERAL", Level = "PRACTICE",
        ContestUsers = new List<ContestUser> { new() { UserId = "teacher", IsOwner = true } }
    };

    private static CurrentUser Teacher(int siteId = 1) => new() { UserId = "teacher", SiteId = siteId, Role = UserRolesEnum.Docente };

    [Fact]
    public async Task Monitor_RejectsUnknownAndNonExamContests()
    {
        _contests.Setup(item => item.GetContestByIdAsync(6, 1)).ReturnsAsync(Contest(isExam: false));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.GetExamMonitorAsync(99, Teacher()));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Service.GetExamMonitorAsync(6, Teacher()));

        Assert.Equal("El concurso no está marcado como examen.", error.Message);
        _contests.Verify(item => item.GetExamActivityAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task Monitor_ReadsActivityFromAnHourBeforeStartUntilEnd()
    {
        var contest = Contest(isExam: true, labIps: "200.87.1.0/24");
        _contests.Setup(item => item.GetContestByIdAsync(5, 1)).ReturnsAsync(contest);
        _contests
            .Setup(item => item.GetExamActivityAsync(5, 1, contest.StartTime, contest.EndTime))
            .ReturnsAsync(new ExamActivity
            {
                ParticipantUserIds = new[] { "ana" },
                Events = new[] { new ExamActivityEvent("ana", "181.1.1.1", contest.StartTime, ExamActivitySources.Login) }
            });

        var result = await Service.GetExamMonitorAsync(5, Teacher());

        Assert.Equal(ExamAlertCodes.OutsideLab, Assert.Single(result.Alerts).Code);
        Assert.Equal(new[] { "200.87.1.0/24" }, result.LabIps);
    }

    [Fact]
    public async Task Monitor_AllowsAnyStaffOfTheSite_NotOnlyTheOwner()
    {
        var contest = Contest(isExam: true);
        _contests.Setup(item => item.GetContestByIdAsync(5, 1)).ReturnsAsync(contest);
        _contests.Setup(item => item.GetExamActivityAsync(5, 1, contest.StartTime, contest.EndTime)).ReturnsAsync(new ExamActivity());

        await Service.GetExamMonitorAsync(5, new CurrentUser { UserId = "other-teacher", SiteId = 1, Role = UserRolesEnum.Docente });
        await Service.GetExamMonitorAsync(5, new CurrentUser { UserId = "aux", SiteId = 1, Role = UserRolesEnum.Auxiliar });
        await Service.GetExamMonitorAsync(5, new CurrentUser { UserId = "admin", SiteId = 1, Role = UserRolesEnum.Administrador });
    }

    [Fact]
    public async Task CreateAndUpdate_NormalizeLabIpsAndRejectInvalidOnes()
    {
        _contests.Setup(item => item.CreateContestAsync(It.IsAny<Contest>(), 1)).ReturnsAsync(new Contest { ContestId = 5 });
        _contests.Setup(item => item.GetContestByIdAsync(5, 1)).ReturnsAsync(Contest(isExam: true));
        var created = Contest(isExam: true, labIps: " 200.87.1.10 \n200.87.1.10\n10.0.0.0/8 ");
        var invalid = Contest(isExam: true, labIps: "sala 3");

        await Service.CreateContestAsync("teacher", created, null!, 1);

        Assert.Equal("200.87.1.10, 10.0.0.0/8", created.ExamLabIps);
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateContestAsync("teacher", invalid, null!, 1));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.UpdateContestAsync(5, Contest(isExam: true, labIps: "999.1.1.1"), null!, 1));
    }
}

public class ExamActivityRepositoryTests
{
    private static readonly DateTime Start = new(2026, 9, 25, 8, 0, 0);

    [Fact]
    public async Task GetExamActivity_CollectsParticipantsSubmissionsAndLoginsInWindow()
    {
        using var seed = new JudgeSeed();
        seed.User("ana", nick: "Ana").User("bob").User("teacher");
        var p = seed.Problem("A");
        var contest = seed.Contest("Parcial", Start, Start.AddHours(2));
        var other = seed.Contest("Otro", Start, Start.AddHours(2));
        seed.ContestUser(contest, "teacher", isOwner: true).ContestUser(contest, "ana").ContestUser(contest, "idle").ContestUser(contest, "zed", siteId: 2);
        var submission = seed.Solution("ana", p, 4, Start.AddMinutes(30), contestId: contest);
        seed.Db.Solutions.Single(item => item.SolutionId == submission).Ip = "181.1.1.1";
        seed.Solution("bob", p, 6, Start.AddMinutes(40), contestId: contest);
        seed.Solution("ana", p, 4, Start.AddMinutes(-10), contestId: contest);
        seed.Solution("ana", p, 4, Start.AddHours(3), contestId: contest);
        seed.Solution("ana", p, 4, Start.AddMinutes(45), contestId: other);
        seed.Solution("ana", p, 4, Start.AddMinutes(50), contestId: contest, siteId: 2);
        seed.Db.Loginlogs.AddRange(
            new DbLoginlog { UserId = "ana", Ip = "181.1.1.1", Time = Start.AddMinutes(-20), SiteId = 1 },
            new DbLoginlog { UserId = "ana", Ip = "190.9.9.9", Time = Start.AddHours(-3), SiteId = 1 },
            new DbLoginlog { UserId = "ana", Ip = "190.8.8.8", Time = Start.AddMinutes(10), SiteId = 2 },
            new DbLoginlog { UserId = "stranger", Ip = "1.2.3.4", Time = Start.AddMinutes(10), SiteId = 1 });
        seed.Save();

        var activity = await new ContestsRepository(seed.Db, CreateMapper()).GetExamActivityAsync(contest, 1, Start, Start.AddHours(2));

        Assert.Equal(new[] { "ana", "bob", "idle" }, activity.ParticipantUserIds.OrderBy(id => id));
        Assert.Equal("Ana", activity.Nicks["ana"]);
        var events = activity.Events.OrderBy(item => item.Time).Select(item => (item.UserId, item.Ip, item.Source)).ToList();
        Assert.Equal(new[]
        {
            ("ana", "181.1.1.1", ExamActivitySources.Login),
            ("ana", "181.1.1.1", ExamActivitySources.Submission),
            ("bob", "10.0.0.1", ExamActivitySources.Submission),
        }, events);
    }

    [Fact]
    public async Task ContestExamFields_PersistOnCreateUpdateAndRead()
    {
        using var seed = new JudgeSeed();
        seed.User("teacher");
        var repository = new ContestsRepository(seed.Db, CreateMapper());
        var created = await repository.CreateContestAsync(new Contest
        {
            Title = "Parcial", StartTime = Start, EndTime = Start.AddHours(2), Defunct = "N", Track = "GENERAL", Level = "PRACTICE",
            IsExam = true, ExamLabIps = "200.87.1.0/24",
            ContestUsers = new List<ContestUser> { new() { UserId = "teacher", IsOwner = true } }
        }, 1);
        seed.Db.ChangeTracker.Clear();

        created.IsExam = false;
        created.ExamLabIps = null;
        created.ContestProblems = new List<ContestProblem>();
        created.ProgrammingLanguages = new List<ProgrammingLanguage>();
        var afterCreate = await repository.GetContestByIdAsync(created.ContestId, 1);
        var listed = (await repository.GetContestsByAuxiliarRoleAsync("aux", 1)).Single();
        seed.Db.ChangeTracker.Clear();
        var updated = await repository.UpdateContestAsync(created.ContestId, created, 1);

        Assert.True(afterCreate.IsExam);
        Assert.Equal("200.87.1.0/24", afterCreate.ExamLabIps);
        Assert.True(listed.IsExam);
        Assert.False(updated.IsExam);
        Assert.Null(updated.ExamLabIps);
    }

    [Fact]
    public async Task ApiLogin_IsRecordedInLoginLogWithIpAndSite()
    {
        using var seed = new JudgeSeed();
        var salt = "ab12";
        var md5 = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes("secret1"))).ToLowerInvariant();
        var digest = System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(md5 + salt));
        seed.Db.Users.Add(new DbUser { UserId = "ana", Password = Convert.ToBase64String(digest.Concat(System.Text.Encoding.UTF8.GetBytes(salt)).ToArray()), Ip = "1", SiteId = 2, IsActive = true });
        seed.Save();

        await seed.PublicRepository().LoginAsync("ana", "secret1", 2, "181.1.1.1");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => seed.PublicRepository().LoginAsync("ana", "wrong", 2, "6.6.6.6"));

        var log = await seed.Db.Loginlogs.AsNoTracking().SingleAsync();
        Assert.Equal("ana", log.UserId);
        Assert.Equal("181.1.1.1", log.Ip);
        Assert.Equal(2, log.SiteId);
        Assert.NotNull(log.Time);
    }
}

public class ExamMonitorEndpointTests
{
    [Fact]
    public async Task ExamMonitor_UsesSiteFromToken()
    {
        var service = new Mock<IContestService>();
        var response = new ExamMonitorResponse { ContestId = 5 };
        service.Setup(item => item.GetExamMonitorAsync(5, It.Is<CurrentUser>(user => user.UserId == "teacher" && user.SiteId == 3))).ReturnsAsync(response);
        var context = AuthenticatedContext("teacher", 3, "Docente");
        var controller = new ContestsController(service.Object, Claims(context), ApiMapper).WithContext(context);

        Assert.Same(response, OkValue(await controller.GetExamMonitorAsync(5)));
    }

    [Fact]
    public void Controller_ReturnsTheCallerPublicIp()
    {
        var context = AuthenticatedContext("teacher", 3, "Docente");
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("172.18.0.2");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.9";
        var controller = new ContestsController(Mock.Of<IContestService>(), Claims(context), ApiMapper).WithContext(context);

        var body = OkValue(controller.GetClientIp())!;

        Assert.Equal("203.0.113.9", body.GetType().GetProperty("ip")!.GetValue(body));
    }

    [Fact]
    public async Task PublicLogin_PassesClientIpToService()
    {
        var service = new Mock<IPublicService>();
        service.Setup(item => item.LoginAsync("ana", "pw", 1, "181.1.1.1")).ReturnsAsync(new PublicAuthenticatedUser { UserId = "ana", Role = "Invitado", SiteId = 1 });
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("172.18.0.2");
        context.Request.Headers["X-Forwarded-For"] = "181.1.1.1";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = "unit-test-signing-key-that-is-long-enough-32b" }).Build();
        var controller = new PublicAuthController(service.Object, Claims(context), configuration).WithContext(context);

        Assert.IsType<OkObjectResult>(await controller.LoginAsync(new OnlineJudgeAdminApi.DataTransferObjects.PublicLoginForCreation { UserId = "ana", Password = "pw", SiteId = 1 }));
    }
}
