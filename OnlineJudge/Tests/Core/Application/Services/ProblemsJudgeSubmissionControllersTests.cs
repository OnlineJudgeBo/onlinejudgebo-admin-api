using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;
using OnlineJudgeAdminApi;
using OnlineJudgeAdminApi.Controllers;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.ExceptionHandler;
using static ControllerTestSupport;

public class ProblemsControllerTests
{
    private readonly Mock<IProblemService> _problems = new();
    private readonly Mock<IProblemPackageService> _packages = new();

    private ProblemsController Controller(string userId = "teacher", int siteId = 2, string role = "Docente")
    {
        var context = AuthenticatedContext(userId, siteId, role);
        return new ProblemsController(_problems.Object, _packages.Object, Claims(context), ApiMapper).WithContext(context);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var claims = Claims(AuthenticatedContext());
        Assert.Throws<ArgumentNullException>(() => new ProblemsController(null!, _packages.Object, claims, ApiMapper));
        Assert.Throws<ArgumentNullException>(() => new ProblemsController(_problems.Object, null!, claims, ApiMapper));
        Assert.Throws<ArgumentNullException>(() => new ProblemsController(_problems.Object, _packages.Object, null!, ApiMapper));
        Assert.Throws<ArgumentNullException>(() => new ProblemsController(_problems.Object, _packages.Object, claims, null!));
    }

    [Fact]
    public async Task GetAll_ListsOrSearches()
    {
        var controller = Controller();

        await controller.GetAllProblemsAsync(null);
        await controller.GetAllProblemsAsync("suma");

        _problems.Verify(item => item.GetAllProblemsAsync(It.Is<CurrentUser>(u => u.UserId == "teacher")), Times.Once);
        _problems.Verify(item => item.SearchProblemAsync(It.Is<CurrentUser>(u => u.UserId == "teacher"), "suma"), Times.Once);
    }

    [Fact]
    public async Task GetById_UsesTokenSite()
    {
        await Controller(siteId: 5).GetProblemByIdAsync(1000);

        _problems.Verify(item => item.GetProblemByIdAsync(1000, 5), Times.Once);
    }

    [Fact]
    public async Task Create_MapsDtoAndUsesTokenUser()
    {
        await Controller("creator", 3).CreateProblemAsync(new ProblemForCreation { Title = "Suma", TimeLimit = 2, MemoryLimit = 256, Spj = "N" });

        _problems.Verify(item => item.CreateProblemAsync("creator", It.Is<Problem>(p => p.Title == "Suma" && p.TimeLimit == 2 && p.MemoryLimit == 256), 3), Times.Once);
    }

    [Fact]
    public async Task Update_ForcesRouteIdAndMapsDto()
    {
        await Controller("editor", 3).UpdateProblemAsync(42, new ProblemForUpdate { Title = "Nuevo", Description = "d" });

        _problems.Verify(item => item.UpdateProblemAsync("editor", 42, It.Is<Problem>(p => p.ProblemId == 42 && p.Title == "Nuevo" && p.Description == "d"), 3), Times.Once);
    }

    [Fact]
    public async Task VisibilityAndDelete_UseSite()
    {
        var controller = Controller(siteId: 4);

        Assert.IsType<OkResult>(await controller.ChangeProblemVisibilityAsync(42));
        Assert.IsType<NoContentResult>(await controller.DeleteProblemByIdAsync(42));

        _problems.Verify(item => item.ChangeProblemVisibilityAsync(42, 4), Times.Once);
        _problems.Verify(item => item.DeleteProblemAsync(42, 4), Times.Once);
    }

    [Fact]
    public async Task Export_ReturnsZipOrMapsErrors()
    {
        _packages.Setup(item => item.ExportProblemPackageAsync(1, 2)).ReturnsAsync(new byte[] { 1, 2, 3 });
        _packages.Setup(item => item.ExportProblemPackageAsync(404, 2)).ThrowsAsync(new KeyNotFoundException("no existe"));
        _packages.Setup(item => item.ExportProblemPackageAsync(400, 2)).ThrowsAsync(new InvalidOperationException("sin datos"));
        var controller = Controller();

        var file = Assert.IsType<FileContentResult>(await controller.ExportProblemAsync(1));
        var notFound = Assert.IsType<NotFoundObjectResult>(await controller.ExportProblemAsync(404));
        var badRequest = Assert.IsType<BadRequestObjectResult>(await controller.ExportProblemAsync(400));

        Assert.Equal("problem-1.zip", file.FileDownloadName);
        Assert.Equal("application/zip", file.ContentType);
        Assert.Equal("no existe", Assert.IsType<ErrorDetails>(notFound.Value).Message);
        Assert.Equal("sin datos", Assert.IsType<ErrorDetails>(badRequest.Value).Message);
    }

    [Fact]
    public async Task Import_RejectsMissingOrEmptyFile()
    {
        var controller = Controller();
        var empty = new FormFile(new MemoryStream(), 0, 0, "file", "p.zip");

        Assert.IsType<BadRequestObjectResult>(await controller.ImportProblemAsync(null!));
        Assert.IsType<BadRequestObjectResult>(await controller.ImportProblemAsync(empty));
        _packages.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Import_PassesStreamUserAndSite()
    {
        var bytes = Encoding.UTF8.GetBytes("zip-bytes");
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "p.zip");
        string? received = null;
        _packages
            .Setup(item => item.ImportProblemPackageAsync("admin", It.IsAny<Stream>(), 6))
            .Callback<string, Stream, int>((_, stream, _) => received = new StreamReader(stream).ReadToEnd())
            .ReturnsAsync(new Problem { ProblemId = 77 });

        var result = await Controller("admin", 6, "Administrador").ImportProblemAsync(file);

        Assert.Equal(77, Assert.IsType<Problem>(OkValue(result)).ProblemId);
        Assert.Equal("zip-bytes", received);
    }
}

public class JudgeControllerTests
{
    private readonly Mock<IJudgeService> _judge = new();
    private readonly Mock<ISolutionService> _solutions = new();

    private JudgeController Controller(HttpContext context) =>
        new JudgeController(_judge.Object, Claims(context), _solutions.Object, ApiMapper).WithContext(context);

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var claims = Claims(AuthenticatedContext());
        Assert.Throws<ArgumentNullException>(() => new JudgeController(null!, claims, _solutions.Object, ApiMapper));
        Assert.Throws<ArgumentNullException>(() => new JudgeController(_judge.Object, null!, _solutions.Object, ApiMapper));
        Assert.Throws<ArgumentNullException>(() => new JudgeController(_judge.Object, claims, null!, ApiMapper));
        Assert.Throws<ArgumentNullException>(() => new JudgeController(_judge.Object, claims, _solutions.Object, null!));
    }

    [Fact]
    public async Task RejudgeEndpoints_UseSiteFromToken()
    {
        var controller = Controller(AuthenticatedContext("teacher", 3, "Docente"));

        await controller.RejudgeSolutionByIdAsync(10);
        await controller.RejudgeSolutionByProblemIdAsync(1000);
        await controller.RejudgeSolutionByContestIdAsync(5);
        await controller.RejudgeSolutionsByRangeAsync(10, 20);
        await controller.RejudgeSolutionsByLanguageAsync(2);
        await controller.GetRejudgeHistoryAsync(25);

        _judge.Verify(item => item.RejudgeSolutionByIdAsync(3, 10), Times.Once);
        _judge.Verify(item => item.RejudgeSolutionByProblemIdAsync(3, 1000), Times.Once);
        _judge.Verify(item => item.RejudgeSolutionByContestIdAsync(3, 5), Times.Once);
        _judge.Verify(item => item.RejudgeSolutionsByRangeAsync(3, 10, 20), Times.Once);
        _judge.Verify(item => item.RejudgeSolutionsByLanguageAsync(3, 2), Times.Once);
        _judge.Verify(item => item.GetRejudgeHistoryAsync(3, 25), Times.Once);
    }

    [Theory]
    [InlineData("Administrador")]
    [InlineData("docente")]
    [InlineData("Auxiliar")]
    public async Task ManualVerdict_AllowedForStaff(string role)
    {
        var result = await Controller(AuthenticatedContext("staff", 3, role)).ManuallyJudgeSolutionAsync(10, new ManualJudgeForUpdate { ResultCode = 4 });

        Assert.IsType<OkObjectResult>(result);
        _judge.Verify(item => item.ManuallyJudgeSolutionAsync(3, 10, 4), Times.Once);
    }

    [Fact]
    public async Task ManualVerdict_AcceptsCommaSeparatedRolesClaim()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "staff"),
                new Claim("site_id", "3"),
                new Claim("roles", "Invitado, Docente")
            }, "TestAuth"))
        };

        Assert.IsType<OkObjectResult>(await Controller(context).ManuallyJudgeSolutionAsync(10, new ManualJudgeForUpdate { ResultCode = 4 }));
    }

    [Fact]
    public async Task ManualVerdict_ForbiddenForStudents()
    {
        var result = await Controller(AuthenticatedContext("student", 3, "Invitado")).ManuallyJudgeSolutionAsync(10, new ManualJudgeForUpdate { ResultCode = 4 });

        Assert.IsType<ForbidResult>(result);
        _judge.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RemoteExecution_MapsRequestUserIpAndSite()
    {
        var context = AuthenticatedContext("client", 3, "Docente");
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("172.18.0.2");
        context.Request.Headers["X-Forwarded-For"] = "10.2.2.2";

        var result = await Controller(context).RemoteExecutionAsync(new RemoteExecutionForCreation
        {
            ClientId = 1, ClientSubmitId = 99, ClientSource = "code", JudgeLanguageId = 2, JudgeProblemId = 1000
        });

        Assert.IsType<OkResult>(result);
        _judge.Verify(item => item.RemoteExecutionAsync(It.Is<RemoteExecutionRequest>(r =>
            r.ClientSubmitId == 99 && r.ClientSource == "code" && r.JudgeLanguageId == 2 && r.JudgeProblemId == 1000 && r.ClientIp == "10.2.2.2"),
            "client", 3), Times.Once);
    }

    [Fact]
    public async Task RemoteExecutionResult_MapsRemoteIdToSolution()
    {
        var result = await Controller(AuthenticatedContext()).RemoteExecutionResult(new RemoteExecutionResult
        {
            RemoteId = 55, Result = 4, Memory = 1024, Time = 10, InDate = "2026-01-01 10:00:00", JudgeTime = "2026-01-01 10:00:05"
        });

        Assert.IsType<OkResult>(result);
        _solutions.Verify(item => item.UpdateSolutionRemoteAsync(It.Is<Solution>(s => s.SolutionId == 55 && s.Result == 4 && s.Memory == 1024)), Times.Once);
    }
}

public class SubmissionControllerTests
{
    private readonly Mock<IAcademicService> _academic = new();
    private readonly Mock<IIdeSubmissionService> _ide = new();
    private readonly Mock<ISolutionService> _solutions = new();

    private SubmissionController Controller(HttpContext? context = null)
    {
        context ??= AuthenticatedContext("ana", 2, "Invitado");
        return new SubmissionController(_academic.Object, _ide.Object, _solutions.Object, Claims(context)).WithContext(context);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var claims = Claims(AuthenticatedContext());
        Assert.Throws<ArgumentNullException>(() => new SubmissionController(null!, _ide.Object, _solutions.Object, claims));
        Assert.Throws<ArgumentNullException>(() => new SubmissionController(_academic.Object, null!, _solutions.Object, claims));
        Assert.Throws<ArgumentNullException>(() => new SubmissionController(_academic.Object, _ide.Object, null!, claims));
        Assert.Throws<ArgumentNullException>(() => new SubmissionController(_academic.Object, _ide.Object, _solutions.Object, null!));
    }

    [Fact]
    public async Task Audit_UsesTokenSiteAndFilters()
    {
        await Controller().GetSubmissionAuditAsync(2, 20, 1000, "bob", "10.0");

        _solutions.Verify(item => item.GetSubmissionAuditAsync(2, 2, 20, 1000, "bob", "10.0"), Times.Once);
    }

    [Fact]
    public async Task Submit_MapsDtoWithClientIp()
    {
        var context = AuthenticatedContext("ana", 2, "Invitado");
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.5");

        await Controller(context).SubmitAsync(new SubmissionForCreation
        {
            ProblemId = 1000, SourceCode = "code", LanguageId = 1, ContestId = 5, CourseId = 3, AssignmentId = 4, FileName = "a.cpp"
        });

        _academic.Verify(item => item.SubmitAsync(It.Is<CurrentUser>(u => u.UserId == "ana"), It.Is<AcademicSubmissionRequest>(r =>
            r.ProblemId == 1000 && r.SourceCode == "code" && r.LanguageId == 1 && r.ContestId == 5
            && r.CourseId == 3 && r.AssignmentId == 4 && r.FileName == "a.cpp" && r.ClientIp == "192.168.1.5")), Times.Once);
    }

    [Fact]
    public async Task IdeEndpoints_PassBearerTokenAndMapRequest()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("172.18.0.2");
        context.Request.Headers.Authorization = "Bearer ide-token";
        context.Request.Headers["X-Forwarded-For"] = "10.9.9.9";
        _ide.Setup(item => item.SubmitAsync("ide-token", It.IsAny<IdeSubmissionRequest>())).ReturnsAsync(new IdeSubmissionResponse { SubmissionId = "7", Id = "7", StatusUrl = "/s/7" });
        _ide.Setup(item => item.CustomInputAsync("ide-token", It.IsAny<IdeSubmissionRequest>())).ReturnsAsync(new IdeRunResponse { RunId = "8", Id = "8", StatusUrl = "/r/8" });
        var dto = new PatitoIdeSubmissionForCreation
        {
            SourceCode = "code", LanguageId = 1, ProblemId = "1000", ContestId = 5, Num = 2, Stdin = "1 2",
            Testcases = new[] { new PatitoIdeTestcaseForCreation { Input = "1", ExpectedOutput = "2" } }
        };
        var controller = Controller(context);

        var submit = Assert.IsType<PatitoIdeSubmissionResponse>(OkValue(await controller.SubmitFromIdeAsync(dto)));
        var run = Assert.IsType<PatitoIdeRunResponse>(OkValue(await controller.RunFromIdeAsync(dto)));
        var custom = Assert.IsType<PatitoIdeRunResponse>(OkValue(await controller.CustomInputFromIdeAsync(dto)));

        Assert.Equal("/s/7", submit.StatusUrl);
        Assert.Equal("8", run.RunId);
        Assert.Equal("/r/8", custom.StatusUrl);
        _ide.Verify(item => item.SubmitAsync("ide-token", It.Is<IdeSubmissionRequest>(r =>
            r.SourceCode == "code" && r.LanguageId == 1 && r.ProblemId == "1000" && r.ContestId == 5 && r.Num == 2 && r.Stdin == "1 2"
            && r.ClientIp == "10.9.9.9" && r.Testcases.Single().Input == "1" && r.Testcases.Single().ExpectedOutput == "2")), Times.Once);
        _ide.Verify(item => item.CustomInputAsync("ide-token", It.IsAny<IdeSubmissionRequest>()), Times.Exactly(2));
    }

    [Fact]
    public async Task IdeStatusEndpoints_MapStatus()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer ide-token";
        _ide.Setup(item => item.GetStatusAsync("ide-token", 7)).ReturnsAsync(new IdeSubmissionStatusResponse
        {
            SubmissionId = "7", Id = "7", Phase = "done", Verdict = "AC", Stdout = "3", Stderr = "", CompileErrors = "",
            Logs = new[] { "ok" }, RuntimeMs = 12, MemoryKb = 900
        });
        var controller = Controller(context);

        var submission = Assert.IsType<PatitoIdeSubmissionStatusResponse>(OkValue(await controller.GetIdeSubmissionStatusAsync(7)));
        var run = Assert.IsType<PatitoIdeSubmissionStatusResponse>(OkValue(await controller.GetIdeRunStatusAsync(7)));

        Assert.Equal("AC", submission.Verdict);
        Assert.Equal(12, submission.RuntimeMs);
        Assert.Equal(900, run.MemoryKb);
        Assert.Equal("ok", run.Logs.Single());
    }

    [Fact]
    public async Task IdeEndpoints_WithoutBearerPassEmptyToken()
    {
        _ide.Setup(item => item.GetStatusAsync("", 7)).ThrowsAsync(new UnauthorizedAccessException());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Controller(new DefaultHttpContext()).GetIdeSubmissionStatusAsync(7));
    }

    [Theory]
    [InlineData("1000", 1000)]
    [InlineData("abc", 0)]
    [InlineData(null, 0)]
    public void PatitoIdeSubmission_ParsesProblemId(string? problemId, int expected)
    {
        Assert.Equal(expected, new PatitoIdeSubmissionForCreation { ProblemId = problemId }.ProblemIdAsInt());
    }
}

public class ExceptionHandlerTests
{
    public static IEnumerable<object[]> Cases => new[]
    {
        new object[] { new ArgumentException("dato malo"), 400, "No se pudo completar la operación: dato malo" },
        new object[] { new ArgumentNullException("x"), 400, "No se pudo completar la operación: Value cannot be null. (Parameter 'x')" },
        new object[] { new InvalidOperationException("estado malo"), 400, "No se pudo completar la operación: estado malo" },
        new object[] { new UnauthorizedAccessException("detalle interno"), 401, "No se pudo completar la operación: solicitud no autorizada" },
        new object[] { new System.Security.SecurityException("solo el dueño"), 403, "No se pudo completar la operación: solo el dueño" },
        new object[] { new KeyNotFoundException("no existe"), 404, "No se pudo completar la operación: no existe" },
        new object[] { new Exception("secreto de base de datos"), 500, "No se pudo completar la operación: error interno" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task MapsExceptionToStatusAndSafeMessage(Exception exception, int status, string message)
    {
        var (context, body) = await Invoke(_ => throw exception);

        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
        Assert.Equal(status, body.GetProperty("statusCode").GetInt32());
        Assert.Equal(message, body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task InternalErrorsDoNotLeakDetails()
    {
        var (_, body) = await Invoke(_ => throw new Exception("password=hunter2"));

        Assert.DoesNotContain("hunter2", body.GetRawText());
    }

    [Fact]
    public async Task PassesThroughWhenNoException()
    {
        var context = new DefaultHttpContext();
        var handler = new ExceptionHandler(ctx => { ctx.Response.StatusCode = 204; return Task.CompletedTask; }, NullLogger<ExceptionHandler>.Instance);

        await handler.InvokeAsync(context);

        Assert.Equal(204, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("Table 'academic.course_user' doesn't exist", 500, "tablas académicas")]
    [InlineData("Table 'academic.learning_path' doesn't exist", 500, "tablas académicas")]
    [InlineData("Table 'jol.solution' doesn't exist", 500, "error interno")]
    [InlineData("Duplicate entry 'x' for key 'course'", 500, "error interno")]
    public async Task MySqlErrors_OnlyMissingAcademicTablesGetSpecificMessage(string mysqlMessage, int status, string expectedFragment)
    {
        var (context, body) = await Invoke(_ => throw CreateMySqlException(mysqlMessage));

        Assert.Equal(status, context.Response.StatusCode);
        Assert.Contains(expectedFragment, body.GetProperty("message").GetString());
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new ExceptionHandler(null!, NullLogger<ExceptionHandler>.Instance));
        Assert.Throws<ArgumentNullException>(() => new ExceptionHandler(_ => Task.CompletedTask, null!));
    }

    private static async Task<(DefaultHttpContext Context, JsonElement Body)> Invoke(RequestDelegate next)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        await new ExceptionHandler(next, NullLogger<ExceptionHandler>.Instance).InvokeAsync(context);
        context.Response.Body.Position = 0;
        return (context, JsonDocument.Parse(context.Response.Body).RootElement.Clone());
    }

    // MySqlException has no public constructor.
    private static MySqlConnector.MySqlException CreateMySqlException(string message)
    {
        var constructor = typeof(MySqlConnector.MySqlException)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .First(item => item.GetParameters().Length >= 1 && item.GetParameters()[0].ParameterType == typeof(string));
        var arguments = constructor.GetParameters()
            .Select((parameter, index) => index == 0 ? message : parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null)
            .ToArray();
        return (MySqlConnector.MySqlException)constructor.Invoke(arguments);
    }
}
