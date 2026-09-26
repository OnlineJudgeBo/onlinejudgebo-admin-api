using System.IO.Compression;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.Controllers;
using OnlineJudgeAdminApi.DataTransferObjects;
using static ControllerTestSupport;

public class PublicControllerTests
{
    private readonly Mock<IPublicService> _service = new();

    private PublicController Controller(HttpContext? context = null)
    {
        context ??= new DefaultHttpContext();
        return new PublicController(_service.Object, Claims(context)).WithContext(context);
    }

    private PublicController Authenticated(int siteId = 2) => Controller(AuthenticatedContext("ana", siteId, "Invitado"));

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new PublicController(null!, Claims(new DefaultHttpContext())));
        Assert.Throws<ArgumentNullException>(() => new PublicController(_service.Object, null!));
    }

    [Fact]
    public async Task AnonymousReads_PassQueryValuesAndNullUser()
    {
        var controller = Controller();

        await controller.GetDashboardAsync(3);
        await controller.GetProblemsAsync(3, 2, 20, "suma", 2024, "icpc", "boca", "dp", "name", 7);
        await controller.GetProblemFiltersAsync(3);
        await controller.GetProblemDetailAsync(1000, 3);
        await controller.GetProblemStatisticsAsync(1000, 3);
        await controller.GetContestProblemDetailAsync(5, "A", 3);
        await controller.GetRankingAsync(3, 10, "global");
        await controller.GetTopicsAsync(3);
        await controller.GetContestsAsync(3, "running", "easy", "start", 2, 12, "icpc");
        await controller.GetContestReportAsync(5, 3);
        await controller.GetLanguagesAsync();
        await controller.GetSubmissionsAsync(3, 1, 25, 5, 1000, "ana", 2, "ac");
        await controller.GetRecentSubmissionsAsync(3, 1, 25);
        await controller.GetContestRecentSubmissionsAsync(5, 3, 1, 25);

        _service.Verify(item => item.GetDashboardAsync(3), Times.Once);
        _service.Verify(item => item.GetProblemsAsync(3, 2, 20, "suma", 2024, "icpc", "boca", "dp", "name", 7, null), Times.Once);
        _service.Verify(item => item.GetProblemFiltersAsync(3), Times.Once);
        _service.Verify(item => item.GetProblemDetailAsync(3, 1000), Times.Once);
        _service.Verify(item => item.GetProblemStatisticsAsync(3, 1000), Times.Once);
        _service.Verify(item => item.GetContestProblemDetailAsync(3, 5, "A", null), Times.Once);
        _service.Verify(item => item.GetRankingAsync(3, 10, "global"), Times.Once);
        _service.Verify(item => item.GetTopicsAsync(3), Times.Once);
        _service.Verify(item => item.GetContestsAsync(3, "running", "easy", "start", 2, 12, "icpc"), Times.Once);
        _service.Verify(item => item.GetContestReportAsync(3, 5, null), Times.Once);
        _service.Verify(item => item.GetLanguagesAsync(), Times.Once);
        _service.Verify(item => item.GetSubmissionsAsync(3, 1, 25, 5, 1000, "ana", 2, "ac", null), Times.Once);
        _service.Verify(item => item.GetRecentSubmissionsAsync(3, 1, 25, null, null, null), Times.Once);
        _service.Verify(item => item.GetRecentSubmissionsAsync(3, 1, 25, 5, null, null), Times.Once);
    }

    [Fact]
    public async Task OptionalUserEndpoints_PassSignedInUser()
    {
        var controller = Authenticated();

        await controller.GetProblemsAsync();
        await controller.GetContestProblemDetailAsync(5, "B");
        await controller.GetContestReportAsync(5);
        await controller.GetSubmissionsAsync();
        await controller.GetContestRecentSubmissionsAsync(5);

        _service.Verify(item => item.GetProblemsAsync(1, 1, 15, null, null, null, null, null, null, null, It.Is<CurrentUser>(u => u.UserId == "ana")), Times.Once);
        _service.Verify(item => item.GetContestProblemDetailAsync(1, 5, "B", It.Is<CurrentUser>(u => u.UserId == "ana")), Times.Once);
        _service.Verify(item => item.GetContestReportAsync(1, 5, It.Is<CurrentUser>(u => u.UserId == "ana")), Times.Once);
        _service.Verify(item => item.GetSubmissionsAsync(1, 1, 25, null, null, null, null, null, It.Is<CurrentUser>(u => u.UserId == "ana")), Times.Once);
        _service.Verify(item => item.GetRecentSubmissionsAsync(1, 1, 25, 5, null, It.Is<CurrentUser>(u => u.UserId == "ana")), Times.Once);
    }

    [Fact]
    public async Task SignedInEndpoints_PreferTokenSiteOverQuery()
    {
        var controller = Authenticated(siteId: 2);

        Assert.IsType<OkResult>(await controller.RegisterForContestAsync(5, siteId: 9));
        await controller.GetOnlineUsersAsync(siteId: 9, windowMinutes: 15);
        await controller.GetCourseRecentSubmissionsAsync(4, siteId: 9, page: 2, pageSize: 10);

        _service.Verify(item => item.RegisterForContestAsync(It.IsAny<CurrentUser>(), 2, 5), Times.Once);
        _service.Verify(item => item.GetOnlineUsersAsync(2, 15), Times.Once);
        _service.Verify(item => item.GetRecentSubmissionsAsync(2, 2, 10, null, 4L, null), Times.Once);
    }

    [Fact]
    public async Task SignedInEndpoints_RejectTokenWithoutSiteSoQuerySiteIsNeverUsed()
    {
        var context = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "ana"),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Invitado")
            }, "TestAuth"))
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Controller(context).GetOnlineUsersAsync(siteId: 9));
        _service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SignedInEndpoints_RejectAnonymous()
    {
        var controller = Controller();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.RegisterForContestAsync(5));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.GetOwnSubmissionsAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.SubmitAsync(new PublicSubmissionForCreation()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.GetSubmissionStatusAsync(1));
        _service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OwnSubmissionsAndStatus_Delegate()
    {
        var controller = Authenticated();

        await controller.GetOwnSubmissionsAsync(3, 50);
        await controller.GetSubmissionStatusAsync(77);

        _service.Verify(item => item.GetOwnSubmissionsAsync(It.Is<CurrentUser>(u => u.UserId == "ana"), 3, 50), Times.Once);
        _service.Verify(item => item.GetSubmissionStatusAsync(It.Is<CurrentUser>(u => u.UserId == "ana"), 77), Times.Once);
    }

    [Fact]
    public async Task Submit_MapsDtoAndUsesForwardedClientIp()
    {
        var context = AuthenticatedContext("ana", 2, "Invitado");
        context.Connection.RemoteIpAddress = IPAddress.Parse("172.18.0.2");
        context.Request.Headers["X-Forwarded-For"] = "10.0.0.7, 172.16.0.1";
        var dto = new PublicSubmissionForCreation
        {
            ProblemId = 1000, ContestProblemId = "A", SourceCode = "int main(){}", LanguageId = 1,
            ContestId = 5, Num = 0, CourseId = 3, AssignmentId = 4, FileName = "a.cpp"
        };

        await Controller(context).SubmitAsync(dto);

        _service.Verify(item => item.SubmitAsync(It.IsAny<CurrentUser>(), It.Is<PublicSubmissionRequest>(r =>
            r.ProblemId == 1000 && r.ContestProblemId == "A" && r.SourceCode == "int main(){}" && r.LanguageId == 1
            && r.ContestId == 5 && r.Num == 0 && r.CourseId == 3 && r.AssignmentId == 4 && r.FileName == "a.cpp"
            && r.ClientIp == "10.0.0.7")), Times.Once);
    }

    [Fact]
    public async Task ContestReportCsv_ForbiddenWhenNotAllowed()
    {
        var result = await Authenticated().GetContestReportCsvAsync(5);

        Assert.IsType<ForbidResult>(result);
        _service.Verify(item => item.GetContestReportAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CurrentUser>()), Times.Never);
    }

    [Fact]
    public async Task ContestReportCsv_WritesRowsWithInvariantNumbersAndEscaping()
    {
        _service.Setup(item => item.CanDownloadContestReportCsvAsync(It.IsAny<CurrentUser>(), 2, 5)).ReturnsAsync(true);
        _service.Setup(item => item.GetContestReportAsync(2, 5, It.IsAny<CurrentUser>())).ReturnsAsync(new ContestReportResponse
        {
            Items = new[]
            {
                new ContestReportItem
                {
                    Rank = 1, UserId = "ana", Nick = "Ana, la \"pro\"", School = "UMSA", Solved = 3, Submissions = 4, Accepted = 3,
                    Accuracy = 0.75m, FirstSubmitUtc = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), LastSubmitUtc = null
                }
            }
        });

        var file = Assert.IsType<FileContentResult>(await Authenticated().GetContestReportCsvAsync(5));
        var lines = Encoding.UTF8.GetString(file.FileContents).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("contest-5-report.csv", file.FileDownloadName);
        Assert.Equal("rank,userId,nick,school,solved,submissions,accepted,accuracy,firstSubmitUtc,lastSubmitUtc", lines[0]);
        Assert.Equal("1,ana,\"Ana, la \"\"pro\"\"\",UMSA,3,4,3,0.75,2026-01-01T10:00:00.0000000Z,", lines[1]);
    }

    [Fact]
    public async Task SourceCodesZip_NotFoundWhenNoSubmissions()
    {
        _service.Setup(item => item.GetOwnSubmissionSourceCodesAsync(It.IsAny<CurrentUser>())).ReturnsAsync(Array.Empty<PublicSubmissionSourceCodeItem>());

        var result = await Authenticated().DownloadOwnSourceCodesAsync();

        var error = Assert.IsType<OnlineJudgeAdminApi.ErrorDetails>(Assert.IsType<NotFoundObjectResult>(result).Value);
        Assert.Equal(404, error.StatusCode);
        Assert.Equal("No hay códigos fuente disponibles para descargar.", error.Message);
    }

    [Theory]
    [InlineData("C++17", ".cpp")]
    [InlineData("GNU CPP", ".cpp")]
    [InlineData("C", ".c")]
    [InlineData("C (gcc)", ".c")]
    [InlineData("Java 17", ".java")]
    [InlineData("Python 3", ".py")]
    [InlineData("JavaScript", ".js")]
    [InlineData("TypeScript", ".ts")]
    [InlineData("C#", ".cs")]
    [InlineData("csharp", ".cs")]
    [InlineData("Go", ".go")]
    [InlineData("Rust", ".rs")]
    [InlineData("Free Pascal", ".pas")]
    [InlineData("Kotlin", ".kt")]
    [InlineData("Brainfuck", ".txt")]
    public async Task SourceCodesZip_UsesExtensionForLanguage(string languageName, string extension)
    {
        var entries = await DownloadZip(Item(1, "Suma", languageName, "ac", "code"));

        Assert.Contains(entries.Keys, name => name.StartsWith("codes/") && name.EndsWith(extension));
    }

    [Fact]
    public async Task SourceCodesZip_ContainsManifestAndSourcesWithSlugifiedNames()
    {
        var entries = await DownloadZip(
            Item(12, "  Árbol, de Búsqueda!! ", "C++", "wa", "int main(){}"),
            Item(3, "???", "Python", "ac", "print(1)"));

        const string first = "codes/00000012-20260102-030405-árbol-de-búsqueda-wa.cpp";
        const string second = "codes/00000003-20260102-030405-problem-ac.py";
        Assert.Equal("int main(){}", entries[first]);
        Assert.Equal("print(1)", entries[second]);
        var manifest = entries["manifest.csv"].Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')).ToList();
        Assert.Equal("solutionId,problemId,problemTitle,languageId,languageName,statusKey,createdAtUtc,fileName", manifest[0]);
        Assert.Equal($"12,1000,\"  Árbol, de Búsqueda!! \",1,C++,wa,2026-01-02T03:04:05.0000000Z,{first}", manifest[1]);
        Assert.Equal($"3,1000,???,1,Python,ac,2026-01-02T03:04:05.0000000Z,{second}", manifest[2]);
    }

    [Fact]
    public async Task SourceCodesZip_FileNameIncludesUser()
    {
        _service.Setup(item => item.GetOwnSubmissionSourceCodesAsync(It.IsAny<CurrentUser>())).ReturnsAsync(new[] { Item(1, "S", "C", "ac", "x") });

        var file = Assert.IsType<FileContentResult>(await Authenticated().DownloadOwnSourceCodesAsync());

        Assert.Equal("application/zip", file.ContentType);
        Assert.Matches("^source-codes-ana-\\d{14}\\.zip$", file.FileDownloadName);
    }

    private async Task<Dictionary<string, string>> DownloadZip(params PublicSubmissionSourceCodeItem[] items)
    {
        _service.Setup(item => item.GetOwnSubmissionSourceCodesAsync(It.IsAny<CurrentUser>())).ReturnsAsync(items);
        var file = Assert.IsType<FileContentResult>(await Authenticated().DownloadOwnSourceCodesAsync());

        using var archive = new ZipArchive(new MemoryStream(file.FileContents));
        return archive.Entries.ToDictionary(entry => entry.FullName, entry =>
        {
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        });
    }

    private static PublicSubmissionSourceCodeItem Item(int solutionId, string title, string language, string status, string source) => new()
    {
        SolutionId = solutionId,
        ProblemId = 1000,
        ProblemTitle = title,
        LanguageId = 1,
        LanguageName = language,
        StatusKey = status,
        CreatedAtUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
        SourceCode = source
    };
}

public class ClientIpHelperTests
{
    [Theory]
    [InlineData("203.0.113.9", "172.18.0.2", "203.0.113.9")]
    [InlineData(" 203.0.113.9 , 10.0.0.1", "172.18.0.2", "203.0.113.9")]
    [InlineData("203.0.113.9", "198.51.100.4", "198.51.100.4")]
    [InlineData(null, "198.51.100.4", "198.51.100.4")]
    [InlineData(null, "::1", "0.0.0.0")]
    [InlineData("2001:db8::1", "172.18.0.2", "0.0.0.0")]
    [InlineData("not-an-ip-but-very-long", "172.18.0.2", "0.0.0.0")]
    [InlineData(" , 10.0.0.1", "172.18.0.2", "0.0.0.0")]
    [InlineData(null, null, "0.0.0.0")]
    public void GetClientIp_NormalizesForwardedOrRemoteAddress(string? forwardedFor, string? remoteIp, string expected)
    {
        var context = new DefaultHttpContext();
        if (forwardedFor != null)
        {
            context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        }

        if (remoteIp != null)
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        }

        Assert.Equal(expected, OnlineJudgeAdminApi.Helpers.ClientIpHelper.GetClientIp(context));
    }
}
