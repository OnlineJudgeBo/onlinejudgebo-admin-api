using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdminApi.Controllers;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Infrastructure;
using static ControllerTestSupport;

public class ContestMachinesServiceTests
{
    private const string Secret = "a-test-secret-that-is-long-enough-32";
    private readonly Mock<IContestsRepository> _contests = new();
    private readonly Mock<IPublicService> _public = new();
    private readonly Mock<IControlGroupStore> _store = new();

    private ContestMachinesService Service(string? secret = Secret) => new(_contests.Object, _public.Object, _store.Object,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ControlServer:TokenSecret"] = secret }).Build());

    private static Contest Exam(int id, bool isExam = true) => new() { ContestId = id, Title = $"Parcial {id}", IsExam = isExam };

    [Fact]
    public async Task Group_IsDerivedFromTheContestAndRegistered()
    {
        _contests.Setup(item => item.GetContestByIdAsync(5, 1)).ReturnsAsync(Exam(5));
        _contests.Setup(item => item.GetContestByIdAsync(6, 1)).ReturnsAsync(Exam(6));

        var first = await Service().GetGroupAsync(5, 1);
        var again = await Service().GetGroupAsync(5, 1);
        var other = await Service().GetGroupAsync(6, 1);

        Assert.Equal("contest-5", first.Id);
        Assert.Equal("Parcial 5", first.Label);
        Assert.Equal(first, again);
        Assert.NotEqual(first.EnrollToken, first.AdminToken);
        Assert.NotEqual(first.AdminToken, other.AdminToken);
        _store.Verify(item => item.EnsureAsync(first), Times.Exactly(2));
    }

    [Fact]
    public async Task Group_RejectsMissingNonExamContestsAndShortSecrets()
    {
        _contests.Setup(item => item.GetContestByIdAsync(7, 1)).ReturnsAsync(Exam(7, isExam: false));
        _contests.Setup(item => item.GetContestByIdAsync(5, 1)).ReturnsAsync(Exam(5));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service().GetGroupAsync(99, 1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().GetGroupAsync(7, 1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service("short").GetGroupAsync(5, 1));
    }

    [Fact]
    public async Task Login_ReturnsTheActiveExamGroupOrAClearReason()
    {
        _public.Setup(item => item.LoginAsync("ana", "ok", 1, "200.1.1.1")).ReturnsAsync(new PublicAuthenticatedUser { UserId = "ana", Nick = "Ana" });
        _public.Setup(item => item.LoginAsync("bob", "ok", 1, "200.1.1.1")).ReturnsAsync(new PublicAuthenticatedUser { UserId = "bob" });
        _public.Setup(item => item.LoginAsync("ana", "bad", 1, "200.1.1.1")).ThrowsAsync(new ArgumentException("Credenciales inválidas."));
        _contests.Setup(item => item.GetActiveExamForUserAsync("ana", 1, It.IsAny<DateTime>())).ReturnsAsync(Exam(5));

        var ok = await Service().LoginAsync("ana", "ok", "200.1.1.1");
        var wrongPassword = await Service().LoginAsync("ana", "bad", "200.1.1.1");
        var noExam = await Service().LoginAsync("bob", "ok", "200.1.1.1");

        Assert.True(ok.Ok);
        Assert.True(ok.Authenticated);
        Assert.Equal("Ana", ok.DisplayName);
        Assert.Equal(5, ok.ContestId);
        Assert.Equal("contest-5", ok.Group!.Id);
        Assert.False(wrongPassword.Ok);
        Assert.False(wrongPassword.Authenticated);
        Assert.Equal("Usuario o contraseña incorrectos.", wrongPassword.Message);
        Assert.False(noExam.Ok);
        Assert.True(noExam.Authenticated);
        Assert.Equal("No tienes un examen activo en este momento.", noExam.Message);
    }
}

public class ControlGroupHttpStoreTests
{
    private sealed class Handler(HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string? Authorization, string Body)> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, request.Headers.Authorization?.ToString(),
                request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(status) { Content = new StringContent("{}") };
        }
    }

    private sealed class Factory(HttpMessageHandler handler, string? baseUrl = "http://control:8090/") : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false)
        {
            BaseAddress = baseUrl == null ? null : new Uri(baseUrl),
        };
    }

    private static ControlGroupHttpStore Store(Handler handler, string? lobby = null, string? url = "http://control:8090/", string? admin = "super-token") =>
        new(new Factory(handler, url), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ControlServer:LobbyEnrollToken"] = lobby,
        ["ControlServer:AdminToken"] = admin,
    }).Build());

    [Fact]
    public async Task Ensure_RegistersTheExamAndOptionalLobbyWithoutExposingTokens()
    {
        var handler = new Handler();

        await Store(handler, lobby: "lobby-token").EnsureAsync(new ControlGroup("contest-5", "Parcial", "enroll-token", "group-admin-token"));

        Assert.Collection(handler.Requests,
            exam =>
            {
                Assert.Equal(HttpMethod.Put, exam.Method);
                Assert.Equal("/admin/groups/contest-5", exam.Path);
                Assert.Equal("Bearer super-token", exam.Authorization);
                Assert.Contains("\"enroll_token\":\"enroll-token\"", exam.Body);
                Assert.Contains("\"admin_token\":\"group-admin-token\"", exam.Body);
                Assert.Contains("\"label\":\"Parcial\"", exam.Body);
            },
            lobby =>
            {
                Assert.Equal("/admin/groups/lobby", lobby.Path);
                Assert.Contains("\"enroll_token\":\"lobby-token\"", lobby.Body);
                Assert.Contains("\"admin_token\":null", lobby.Body);
            });
    }

    [Theory]
    [InlineData(null, "super-token")]
    [InlineData("http://control:8090/", null)]
    public async Task Ensure_RequiresTheControlServerConfiguration(string? url, string? admin)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store(new Handler(), url: url, admin: admin)
            .EnsureAsync(new ControlGroup("contest-1", "A", "enroll", "admin")));
    }

    [Fact]
    public async Task Ensure_ReportsARejectedGroup()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store(new Handler(HttpStatusCode.BadRequest))
            .EnsureAsync(new ControlGroup("contest-1", "A", "enroll", "admin")));
    }
}

public class ContestMachinesControllerTests
{
    private static readonly ControlGroup Group = new("contest-5", "Parcial", "enroll-tok", "admin-tok");

    // Answers GET admin/machines with this group's machines and records every other call.
    private sealed class StubHandler : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string? Body)> Forwarded { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == "/admin/machines")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"machines\":[{\"machine_id\":\"m1\"}]}") };
            }

            Forwarded.Add((request, request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Factory(HttpMessageHandler handler, string? baseUrl) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = baseUrl == null ? null : new Uri(baseUrl) };
    }

    private static (ContestMachinesController Controller, StubHandler Handler) Controller(
        string method = "GET", string body = "", string query = "?group=contest-5", string? baseUrl = "http://control:8090/", string? superadminToken = "super-tok")
    {
        var machines = new Mock<IContestMachinesService>();
        machines.Setup(item => item.GetGroupAsync(5, 3)).ReturnsAsync(Group);
        var handler = new StubHandler();
        var context = AuthenticatedContext("teacher", 3, "Auxiliar");
        context.Request.Method = method;
        context.Request.QueryString = new QueryString(query);
        context.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/json";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ControlServer:AdminToken"] = superadminToken }).Build();
        return (new ContestMachinesController(machines.Object, new Factory(handler, baseUrl), configuration, Claims(context)).WithContext(context), handler);
    }

    [Fact]
    public async Task Forward_UsesTheGroupTokenAndKeepsPathQueryAndBody()
    {
        var (controller, handler) = Controller("POST", "{\"action\":\"message\",\"target\":{\"group_id\":\"contest-5\"}}");

        var result = Assert.IsType<FileStreamResult>(await controller.ForwardAsync(5, "cmd"));

        var (request, body) = Assert.Single(handler.Forwarded);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://control:8090/admin/cmd?group=contest-5", request.RequestUri!.ToString());
        Assert.Equal("Bearer admin-tok", request.Headers.Authorization!.ToString());
        Assert.Contains("\"action\":\"message\"", body);
        Assert.StartsWith("application/json", result.ContentType);
    }

    [Theory]
    [InlineData("{\"action\":\"net-open\",\"target\":{\"group_id\":\"contest-5\"}}")]
    [InlineData("{\"action\":\"collect-home\",\"target\":{\"machine_id\":\"m1\"}}")]
    [InlineData("{\"action\":\"usb-block\",\"target\":{\"group_id\":\"contest-5\",\"machine_id\":\"*\"}}")]
    public async Task SuperadminCommands_ForThisExamUseTheSuperadminToken(string command)
    {
        var (controller, handler) = Controller("POST", command);

        Assert.IsType<FileStreamResult>(await controller.ForwardAsync(5, "cmd"));

        Assert.Equal("Bearer super-tok", Assert.Single(handler.Forwarded).Request.Headers.Authorization!.ToString());
    }

    [Theory]
    [InlineData("{\"action\":\"net-open\",\"target\":{\"group_id\":\"contest-6\"}}")]
    [InlineData("{\"action\":\"usb-unblock\",\"target\":{\"machine_id\":\"m9\"}}")]
    [InlineData("{\"action\":\"set-allowlist\",\"target\":{\"all\":true},\"args\":{\"hosts\":[]}}")]
    [InlineData("{\"action\":\"net-open\",\"target\":{}}")]
    [InlineData("{\"action\":\"net-open\",\"target\":{\"machine_id\":\"*\"}}")]
    [InlineData("{\"action\":\"net-open\",\"target\":{\"group_id\":\"contest-6\",\"machine_id\":\"*\"}}")]
    public async Task SuperadminCommands_OutsideThisExamAreRefused(string command)
    {
        var (controller, handler) = Controller("POST", command);

        var result = Assert.IsType<ObjectResult>(await controller.ForwardAsync(5, "cmd"));

        Assert.Equal(403, result.StatusCode);
        Assert.Empty(handler.Forwarded);
    }

    [Fact]
    public async Task Allowlist_IsPinnedToThisExamGroup()
    {
        var (getController, getHandler) = Controller(query: "?group=contest-6");
        var (putController, putHandler) = Controller("PUT", "{\"group_id\":\"contest-6\",\"hosts\":[\"juez.example\"]}", query: "");

        await getController.ForwardAsync(5, "allowlist");
        await putController.ForwardAsync(5, "allowlist");

        var get = Assert.Single(getHandler.Forwarded).Request;
        Assert.Equal("http://control:8090/admin/allowlist?group=contest-5", get.RequestUri!.ToString());
        Assert.Equal("Bearer super-tok", get.Headers.Authorization!.ToString());
        var put = Assert.Single(putHandler.Forwarded);
        Assert.Contains("\"group_id\":\"contest-5\"", put.Body);
        Assert.Contains("juez.example", put.Body);
    }

    [Fact]
    public async Task SuperadminOperations_NeedTheSuperadminToken()
    {
        var (controller, handler) = Controller(superadminToken: null);

        var result = Assert.IsType<ObjectResult>(await controller.ForwardAsync(5, "allowlist"));

        Assert.Equal(503, result.StatusCode);
        Assert.Empty(handler.Forwarded);
    }

    [Theory]
    [InlineData("credentials")]
    [InlineData("events")]
    [InlineData("session")]
    [InlineData("teams")]
    [InlineData("phase")]
    [InlineData("machines/../../enroll")]
    [InlineData("")]
    public async Task Forward_RejectsCredentialsEventsAndPathTricks(string path)
    {
        var (controller, handler) = Controller();

        Assert.IsType<NotFoundResult>(await controller.ForwardAsync(5, path));
        Assert.Empty(handler.Forwarded);
    }

    [Fact]
    public async Task Forward_ReportsAMissingControlServer()
    {
        var (controller, _) = Controller(baseUrl: null);

        var result = Assert.IsType<ObjectResult>(await controller.ForwardAsync(5, "machines"));

        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public async Task Group_ReturnsIdAndLabelWithoutTokens()
    {
        var (controller, _) = Controller();

        var body = OkValue(await controller.GetGroupAsync(5))!;

        Assert.Equal("contest-5", body.GetType().GetProperty("groupId")!.GetValue(body));
        Assert.Null(body.GetType().GetProperty("adminToken"));
    }
}

public class LabLoginControllerTests
{
    private sealed class SettingsHandler(string homepageJson, string logoJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri!.AbsolutePath.EndsWith("/homepage") ? homepageJson : logoJson),
            });
    }

    private static readonly LabLoginResult Success = new()
    {
        Ok = true, Authenticated = true, UserId = "ana", DisplayName = "Ana", ContestId = 5,
        Group = new ControlGroup("contest-5", "Parcial", "enroll-tok", "admin-tok"),
    };

    private static LabLoginController Controller(LabLoginResult result, HttpMessageHandler? controlServer = null)
    {
        var machines = new Mock<IContestMachinesService>();
        machines.Setup(item => item.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(result);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(item => item.CreateClient(It.IsAny<string>())).Returns(controlServer == null
            ? new HttpClient()
            : new HttpClient(controlServer) { BaseAddress = new Uri("http://control:8090/") });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Base:Url"] = "https://juez.example/",
            ["ControlServer:LogoUrl"] = "https://juez.example/control/icpc-bolivia-logo.svg",
        }).Build();
        return new LabLoginController(machines.Object, factory.Object, configuration, new MemoryCache(new MemoryCacheOptions())).WithContext(new DefaultHttpContext());
    }

    private static object? Prop(object body, string name) => body.GetType().GetProperty(name)?.GetValue(body);

    [Fact]
    public async Task Login_AnswersTheIsoContractWithTheExamAsRegion()
    {
        var controller = Controller(new LabLoginResult
        {
            Ok = true, Authenticated = true, UserId = "ana maría", DisplayName = "Ana", ContestId = 5,
            Group = new ControlGroup("contest-5", "Parcial", "enroll-tok", "admin-tok"),
        });

        var body = OkValue(await controller.LoginAsync(new LabLoginRequest { Username = " ana maría ", Password = "x" }))!;

        Assert.Equal(true, Prop(body, "ok"));
        Assert.Equal("https://juez.example/oj/contest.php?cid=5", Prop(body, "homepage"));
        Assert.Equal("ana-mar-a", Prop(Prop(body, "team")!, "id"));
        Assert.Equal("Parcial · Ana", Prop(Prop(body, "team")!, "name"));
        Assert.Equal("https://juez.example/control/icpc-bolivia-logo.svg", Prop(body, "logoUrl"));
        var region = Prop(body, "region")!;
        Assert.Equal("contest-5", Prop(region, "id"));
        Assert.Equal("enroll-tok", Prop(region, "enrollToken"));
        Assert.Null(Prop(region, "adminToken"));
    }

    [Fact]
    public async Task Login_IgnoresTheControlServerDefaultHomepageButUsesItsLogo()
    {
        var controller = Controller(Success, new SettingsHandler(
            "{\"url\": \"file:///usr/share/doc/contest/index.html\", \"updated_at\": null}",
            "{\"url\": \"\", \"effective_url\": \"https://cdn.example/logo.svg\"}"));

        var body = OkValue(await controller.LoginAsync(new LabLoginRequest { Username = "ana", Password = "x" }))!;

        Assert.Equal("https://juez.example/oj/contest.php?cid=5", Prop(body, "homepage"));
        Assert.Equal("https://cdn.example/logo.svg", Prop(body, "logoUrl"));
    }

    [Fact]
    public async Task Login_UsesAHomepageSetForTheExam()
    {
        var controller = Controller(Success, new SettingsHandler(
            "{\"url\": \"https://juez.example/oj/problemset.php\", \"updated_at\": \"2026-09-26T10:00:00Z\"}",
            "{\"url\": \"\", \"effective_url\": \"\"}"));

        var body = OkValue(await controller.LoginAsync(new LabLoginRequest { Username = "ana", Password = "x" }))!;

        Assert.Equal("https://juez.example/oj/problemset.php", Prop(body, "homepage"));
        // No logo set for the exam: the Patito one.
        Assert.Equal("https://juez.example/control/icpc-bolivia-logo.svg", Prop(body, "logoUrl"));
    }

    [Fact]
    public async Task Login_FailureIsA200WithOkFalse()
    {
        var controller = Controller(new LabLoginResult { Authenticated = true, Message = "No tienes un examen activo en este momento." });

        var body = OkValue(await controller.LoginAsync(new LabLoginRequest { Username = "bob", Password = "x" }))!;

        Assert.Equal(false, Prop(body, "ok"));
        Assert.Equal("No tienes un examen activo en este momento.", Prop(body, "message"));
    }

    [Fact]
    public async Task Login_BlocksAfterTenCredentialFailures()
    {
        var controller = Controller(new LabLoginResult { Message = "Usuario o contraseña incorrectos." });
        var request = new LabLoginRequest { Username = "rate-limit-test", Password = "bad" };

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var body = OkValue(await controller.LoginAsync(request))!;
            Assert.Equal("Usuario o contraseña incorrectos.", Prop(body, "message"));
        }

        var blocked = OkValue(await controller.LoginAsync(request))!;
        Assert.Equal("Demasiados intentos. Espera unos minutos.", Prop(blocked, "message"));
    }
}

public class ActiveExamRepositoryTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 10, 0, 0);

    private static int Exam(JudgeSeed seed, string title, int startHoursAgo, int endHoursAhead, int siteId = 1, bool isExam = true, string defunct = "N")
    {
        var id = seed.Contest(title, Now.AddHours(-startHoursAgo), Now.AddHours(endHoursAhead), siteId, defunct: defunct);
        seed.Db.Contests.Find(id)!.IsExam = isExam;
        seed.Save();
        return id;
    }

    [Fact]
    public async Task ActiveExam_IsTheRunningExamTheUserTakesLatestStartFirst()
    {
        using var seed = new JudgeSeed();
        seed.User("ana").User("teacher").Site(2);
        var older = Exam(seed, "Parcial 1", startHoursAgo: 2, endHoursAhead: 2);
        var newer = Exam(seed, "Recuperatorio", startHoursAgo: 1, endHoursAhead: 1);
        var finished = Exam(seed, "Terminado", startHoursAgo: 5, endHoursAhead: -1);
        var practice = Exam(seed, "Practica", startHoursAgo: 0, endHoursAhead: 1, isExam: false);
        var deleted = Exam(seed, "Borrado", startHoursAgo: 0, endHoursAhead: 1, defunct: "Y");
        var otherSite = Exam(seed, "Otro sitio", startHoursAgo: 0, endHoursAhead: 1, siteId: 2);
        foreach (var contest in new[] { older, newer, finished, practice, deleted })
        {
            seed.ContestUser(contest, "ana");
        }
        seed.ContestUser(otherSite, "ana", siteId: 2).ContestUser(newer, "teacher", isOwner: true);
        var repository = new ContestsRepository(seed.Db, RepositoryTestSupport.CreateMapper());

        var active = await repository.GetActiveExamForUserAsync("ana", 1, Now);

        Assert.Equal(newer, active!.ContestId);
        Assert.Equal("Recuperatorio", active.Title);
        Assert.Null(await repository.GetActiveExamForUserAsync("teacher", 1, Now));
        Assert.Null(await repository.GetActiveExamForUserAsync("ana", 1, Now.AddHours(3)));
        Assert.Equal(otherSite, (await repository.GetActiveExamForUserAsync("ana", 2, Now))!.ContestId);
    }
}
