using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.FileSystemLocalManager;
using OnlineJudgeAdminApi.Controllers;
using OnlineJudgeAdminApi.DataTransferObjects;
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
        Assert.Equal("Ana", ok.DisplayName);
        Assert.Equal(5, ok.ContestId);
        Assert.Equal("contest-5", ok.Group!.Id);
        Assert.False(wrongPassword.Ok);
        Assert.Equal("Usuario o contraseña incorrectos.", wrongPassword.Message);
        Assert.False(noExam.Ok);
        Assert.Equal("No tienes un examen activo en este momento.", noExam.Message);
    }
}

public class ControlGroupFileStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "control-groups-" + Guid.NewGuid().ToString("N"));
    private string File => Path.Combine(_dir, "groups.json");

    private ControlGroupFileStore Store(string? lobby = null) => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ControlServer:GroupsFile"] = File,
        ["ControlServer:LobbyEnrollToken"] = lobby,
    }).Build());

    [Fact]
    public async Task Ensure_AddsTheGroupAndKeepsTheOthers()
    {
        Directory.CreateDirectory(_dir);
        await System.IO.File.WriteAllTextAsync(File, "{\"sede-icpc\": \"tok\"}");

        await Store(lobby: "lobby-tok").EnsureAsync(new ControlGroup("contest-5", "Parcial", "enroll", "admin"));
        await Store(lobby: "lobby-tok").EnsureAsync(new ControlGroup("contest-5", "Parcial", "enroll", "admin"));

        var groups = JsonNode.Parse(await System.IO.File.ReadAllTextAsync(File))!.AsObject();
        Assert.Equal("tok", groups["sede-icpc"]!.GetValue<string>());
        Assert.Equal("enroll", groups["contest-5"]!["enroll_token"]!.GetValue<string>());
        Assert.Equal("admin", groups["contest-5"]!["admin_token"]!.GetValue<string>());
        Assert.Equal("Parcial", groups["contest-5"]!["label"]!.GetValue<string>());
        Assert.Equal("lobby-tok", groups["lobby"]!["enroll_token"]!.GetValue<string>());
        Assert.False(System.IO.File.Exists(File + ".tmp"));
    }

    [Fact]
    public async Task Ensure_CreatesTheFileAndRequiresAPath()
    {
        await Store().EnsureAsync(new ControlGroup("contest-1", "A", "e", "a"));

        Assert.True(System.IO.File.Exists(File));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ControlGroupFileStore(new ConfigurationBuilder().Build()).EnsureAsync(new ControlGroup("contest-1", "A", "e", "a")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}

public class ContestMachinesControllerTests
{
    private static readonly ControlGroup Group = new("contest-5", "Parcial", "enroll-tok", "admin-tok");

    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK) { Content = new StringContent("{\"machines\":[]}", System.Text.Encoding.UTF8, "application/json") };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return Response;
        }
    }

    private sealed class Factory(HttpMessageHandler handler, string? baseUrl) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = baseUrl == null ? null : new Uri(baseUrl) };
    }

    private static (ContestMachinesController Controller, StubHandler Handler) Controller(string method = "GET", string body = "", string? baseUrl = "http://control:8090/")
    {
        var machines = new Mock<IContestMachinesService>();
        machines.Setup(item => item.GetGroupAsync(5, 3)).ReturnsAsync(Group);
        var handler = new StubHandler();
        var context = AuthenticatedContext("teacher", 3, "Auxiliar");
        context.Request.Method = method;
        context.Request.QueryString = new QueryString("?group=contest-5");
        context.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/json";
        return (new ContestMachinesController(machines.Object, new Factory(handler, baseUrl), Claims(context)).WithContext(context), handler);
    }

    [Fact]
    public async Task Forward_UsesTheGroupTokenAndKeepsPathQueryAndBody()
    {
        var (controller, handler) = Controller("POST", "{\"action\":\"lock\"}");

        var result = Assert.IsType<FileStreamResult>(await controller.ForwardAsync(5, "cmd"));

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("http://control:8090/admin/cmd?group=contest-5", handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer admin-tok", handler.Request.Headers.Authorization!.ToString());
        Assert.Equal("{\"action\":\"lock\"}", handler.Body);
        Assert.StartsWith("application/json", result.ContentType);
        Assert.Equal(200, controller.Response.StatusCode);
    }

    [Theory]
    [InlineData("credentials")]
    [InlineData("events")]
    [InlineData("machines/../../enroll")]
    [InlineData("")]
    public async Task Forward_RejectsCredentialsEventsAndPathTricks(string path)
    {
        var (controller, handler) = Controller();

        Assert.IsType<NotFoundResult>(await controller.ForwardAsync(5, path));
        Assert.Null(handler.Request);
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
        Ok = true, UserId = "ana", DisplayName = "Ana", ContestId = 5,
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
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Base:Url"] = "https://juez.example/" }).Build();
        return new LabLoginController(machines.Object, factory.Object, configuration).WithContext(new DefaultHttpContext());
    }

    private static object? Prop(object body, string name) => body.GetType().GetProperty(name)?.GetValue(body);

    [Fact]
    public async Task Login_AnswersTheIsoContractWithTheExamAsRegion()
    {
        var controller = Controller(new LabLoginResult
        {
            Ok = true, UserId = "ana maría", DisplayName = "Ana", ContestId = 5,
            Group = new ControlGroup("contest-5", "Parcial", "enroll-tok", "admin-tok"),
        });

        var body = OkValue(await controller.LoginAsync(new LabLoginRequest { Username = " ana maría ", Password = "x" }))!;

        Assert.Equal(true, Prop(body, "ok"));
        Assert.Equal("https://juez.example/oj/contest.php?cid=5", Prop(body, "homepage"));
        Assert.Equal("ana-mar-a", Prop(Prop(body, "team")!, "id"));
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
        Assert.Equal(string.Empty, Prop(body, "logoUrl"));
    }

    [Fact]
    public async Task Login_FailureIsA200WithOkFalse()
    {
        var controller = Controller(new LabLoginResult { Message = "No tienes un examen activo en este momento." });

        var body = OkValue(await controller.LoginAsync(new LabLoginRequest { Username = "bob", Password = "x" }))!;

        Assert.Equal(false, Prop(body, "ok"));
        Assert.Equal("No tienes un examen activo en este momento.", Prop(body, "message"));
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
