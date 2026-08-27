using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
using static RepositoryTestSupport;

// PublicRepository is the public-facing (unauthenticated/self-service) API:
// registration, login, password reset, contest registration, submission.
// SubmitAsync/RegisterAsync use real transactions and LoginAsync/
// ResetPasswordWithTokenAsync use ExecuteUpdateAsync, none of which the
// InMemory provider supports, so this whole file runs on Sqlite for
// consistency rather than mixing providers per test.
public class PublicRepositoryTests
{
    private static string Md5Hex(string value)
    {
        using var md5 = MD5.Create();
        return Convert.ToHexString(md5.ComputeHash(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static PublicRepository CreateRepository(AppDbContext context) =>
        new(context, CreateAcademicContext());

    private static async Task SeedUserAsync(AppDbContext context, string userId, int siteId, string? password = null, bool active = true)
    {
        context.Users.Add(new DbUser
        {
            UserId = userId,
            Password = password,
            Ip = "127.0.0.1",
            SiteId = siteId,
            IsActive = active,
            IsDeleted = false
        });
        await context.SaveChangesAsync();
    }

    private static async Task<int> SeedProblemAsync(AppDbContext context, int siteId)
    {
        if (!await context.Sites.AnyAsync(site => site.SiteId == siteId))
        {
            context.Sites.Add(new DbSite { SiteId = siteId, Name = "Site " + siteId });
        }
        var problem = new DbProblem { Title = "Problem A", Spj = "N", Defunct = "N", TimeLimit = 1000, MemoryLimit = 128 };
        context.Problems.Add(problem);
        await context.SaveChangesAsync();
        context.ProblemSites.Add(new DbProblemSite { problemId = problem.ProblemId!.Value, SiteId = siteId, IsActive = true });
        await context.SaveChangesAsync();
        return problem.ProblemId!.Value;
    }

    // ---- RegisterForContestAsync -----------------------------------------

    [Fact]
    public async Task RegisterForContestAsync_RegistersUser_AndIsIdempotent()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = CreateRepository(context);
        context.Sites.Add(new DbSite { SiteId = 1, Name = "Site 1" });
        context.Contests.Add(new DbContest { ContestId = 1, Title = "Contest A", StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddDays(1), Defunct = "N", Private = 0 });
        await context.SaveChangesAsync();
        context.ContestSites.Add(new DbContestSite { ContestId = 1, SiteId = 1 });
        await SeedUserAsync(context, "alice", siteId: 1);
        var currentUser = new CurrentUser { UserId = "alice", SiteId = 1, Role = UserRolesEnum.Invitado };

        await repository.RegisterForContestAsync(currentUser, siteId: 1, contestId: 1);
        await repository.RegisterForContestAsync(currentUser, siteId: 1, contestId: 1);

        Assert.Single(await context.ContestUsers.ToListAsync());
    }

    [Fact]
    public async Task RegisterForContestAsync_ThrowsForPrivateContest_WhenUserNotAuthorized()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = CreateRepository(context);
        context.Sites.Add(new DbSite { SiteId = 1, Name = "Site 1" });
        context.Contests.Add(new DbContest { ContestId = 1, Title = "Contest A", StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddDays(1), Defunct = "N", Private = 1 });
        await context.SaveChangesAsync();
        context.ContestSites.Add(new DbContestSite { ContestId = 1, SiteId = 1 });
        await context.SaveChangesAsync();
        var currentUser = new CurrentUser { UserId = "alice", SiteId = 1, Role = UserRolesEnum.Invitado };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            repository.RegisterForContestAsync(currentUser, siteId: 1, contestId: 1));
    }

    // ---- SubmitAsync ------------------------------------------------------

    [Fact]
    public async Task SubmitAsync_PersistsSolutionAndSourceWithMatchingSolutionIdFk()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = CreateRepository(context);
        await SeedUserAsync(context, "alice", siteId: 1);
        int problemId = await SeedProblemAsync(context, siteId: 1);
        context.ProgrammingLanguages.Add(new DbProgrammingLanguage { LanguageId = 54, Name = "C++" });
        await context.SaveChangesAsync();
        var currentUser = new CurrentUser { UserId = "alice", SiteId = 1, Role = UserRolesEnum.Invitado };

        PublicSubmissionResponse response = await repository.SubmitAsync(currentUser,
            new PublicSubmissionRequest { ProblemId = problemId, SourceCode = "print(1)" }, languageId: 54);

        Assert.NotEqual(0, response.SolutionId);
        DbSourceCode source = await context.SourceCodes.SingleAsync();
        Assert.Equal(response.SolutionId, source.SolutionId);
    }

    [Fact]
    public async Task SubmitAsync_ThrowsWhenUserInvalidForSite()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = CreateRepository(context);
        int problemId = await SeedProblemAsync(context, siteId: 1);
        var currentUser = new CurrentUser { UserId = "nobody", SiteId = 1, Role = UserRolesEnum.Invitado };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.SubmitAsync(currentUser, new PublicSubmissionRequest { ProblemId = problemId, SourceCode = "x" }, languageId: 54));
    }

    [Fact]
    public async Task SubmitAsync_ThrowsWhenProblemNotAvailableForSite()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = CreateRepository(context);
        await SeedUserAsync(context, "alice", siteId: 1);
        var currentUser = new CurrentUser { UserId = "alice", SiteId = 1, Role = UserRolesEnum.Invitado };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.SubmitAsync(currentUser, new PublicSubmissionRequest { ProblemId = 9999, SourceCode = "x" }, languageId: 54));
    }

    [Fact]
    public async Task SubmitAsync_ThrowsWhenLanguageUnsupported()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = CreateRepository(context);
        await SeedUserAsync(context, "alice", siteId: 1);
        int problemId = await SeedProblemAsync(context, siteId: 1);
        var currentUser = new CurrentUser { UserId = "alice", SiteId = 1, Role = UserRolesEnum.Invitado };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.SubmitAsync(currentUser, new PublicSubmissionRequest { ProblemId = problemId, SourceCode = "x" }, languageId: 9999));
    }

    // ---- LoginAsync --------------------------------------------------------

    [Fact]
    public async Task LoginAsync_UpdatesAccessTime_ForValidCredentials()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = CreateRepository(context);
        await SeedUserAsync(context, "alice", siteId: 1, password: Md5Hex("secret"));

        PublicAuthenticatedUser result = await repository.LoginAsync("alice", "secret", siteId: 1);

        Assert.Equal("alice", result.UserId);
        // AsNoTracking: LoginAsync updates via ExecuteUpdateAsync, which
        // bypasses the change tracker entirely, so a tracked query here
        // would just return the stale pre-update in-memory instance.
        Assert.NotNull((await context.Users.AsNoTracking().SingleAsync()).Accesstime);
    }

    [Fact]
    public async Task LoginAsync_ThrowsForWrongPassword()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = CreateRepository(context);
        await SeedUserAsync(context, "alice", siteId: 1, password: Md5Hex("secret"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            repository.LoginAsync("alice", "wrong", siteId: 1));
    }

    [Fact]
    public async Task LoginAsync_ThrowsForInactiveUser()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = CreateRepository(context);
        await SeedUserAsync(context, "alice", siteId: 1, password: Md5Hex("secret"), active: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            repository.LoginAsync("alice", "secret", siteId: 1));
    }

    // ---- RegisterAsync ------------------------------------------------------

    [Fact]
    public async Task RegisterAsync_PersistsUserProfileAndActivity()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = CreateRepository(context);

        PublicAuthenticatedUser result = await repository.RegisterAsync(
            "alice", "hash", "alice@example.com", "Alice", "Doe", "School", siteId: 1, ipAddress: "127.0.0.1");

        Assert.Equal("alice", result.UserId);
        DbUser user = await context.Users.SingleAsync();
        Assert.Equal("hash", user.Password);
        DbUserProfile profile = await context.UserProfiles.SingleAsync();
        Assert.Equal("alice@example.com", profile.Email);
        DbUserActivity activity = await context.UserActivities.SingleAsync();
        Assert.Equal("alice", activity.UserId);
    }

    [Fact]
    public async Task RegisterAsync_ThrowsWhenUserIdAlreadyExists()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = CreateRepository(context);
        await SeedUserAsync(context, "alice", siteId: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.RegisterAsync("alice", "hash", "new@example.com", null, null, null, siteId: 1, ipAddress: "127.0.0.1"));
    }

    [Fact]
    public async Task RegisterAsync_ThrowsWhenEmailAlreadyRegistered()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = CreateRepository(context);
        await repository.RegisterAsync("alice", "hash", "shared@example.com", null, null, null, siteId: 1, ipAddress: "127.0.0.1");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.RegisterAsync("bob", "hash", "shared@example.com", null, null, null, siteId: 1, ipAddress: "127.0.0.1"));
    }

    // ---- Password recovery -------------------------------------------------

    [Fact]
    public async Task SavePasswordRecoveryTokenAsync_SetsTokenAndExpiry()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = CreateRepository(context);
        await SeedUserAsync(context, "alice", siteId: 1);
        DateTime expiresAt = DateTime.UtcNow.AddHours(1);

        await repository.SavePasswordRecoveryTokenAsync("alice", siteId: 1, tokenHash: "token123", expiresAtUtc: expiresAt);

        DbUser user = await context.Users.SingleAsync();
        Assert.Equal("token123", user.ResetPasswordToken);
    }

    [Fact]
    public async Task SavePasswordRecoveryTokenAsync_ThrowsWhenUserNotFound()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = CreateRepository(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            repository.SavePasswordRecoveryTokenAsync("nobody", siteId: 1, tokenHash: "x", expiresAtUtc: DateTime.UtcNow));
    }

    [Fact]
    public async Task ResetPasswordWithTokenAsync_UpdatesPassword_AndClearsToken_ForValidToken()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = CreateRepository(context);
        await SeedUserAsync(context, "alice", siteId: 1);
        context.UserProfiles.Add(new DbUserProfile { UserId = "alice", SiteId = 1, Email = "alice@example.com", Nick = "alice" });
        await context.SaveChangesAsync();
        await repository.SavePasswordRecoveryTokenAsync("alice", siteId: 1, tokenHash: "token123", expiresAtUtc: DateTime.UtcNow.AddHours(1));

        bool result = await repository.ResetPasswordWithTokenAsync("alice@example.com", siteId: 1, tokenHash: "token123", nowUtc: DateTime.UtcNow, passwordHash: "newhash");

        Assert.True(result);
        // AsNoTracking: same reason as LoginAsync above — ExecuteUpdateAsync
        // bypasses the change tracker.
        DbUser user = await context.Users.AsNoTracking().SingleAsync();
        Assert.Equal("newhash", user.Password);
        Assert.Null(user.ResetPasswordToken);
    }

    [Fact]
    public async Task ResetPasswordWithTokenAsync_ReturnsFalse_ForExpiredToken()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = CreateRepository(context);
        await SeedUserAsync(context, "alice", siteId: 1);
        context.UserProfiles.Add(new DbUserProfile { UserId = "alice", SiteId = 1, Email = "alice@example.com", Nick = "alice" });
        await context.SaveChangesAsync();
        await repository.SavePasswordRecoveryTokenAsync("alice", siteId: 1, tokenHash: "token123", expiresAtUtc: DateTime.UtcNow.AddHours(-1));

        bool result = await repository.ResetPasswordWithTokenAsync("alice@example.com", siteId: 1, tokenHash: "token123", nowUtc: DateTime.UtcNow, passwordHash: "newhash");

        Assert.False(result);
    }

    // ---- GetProblemFiltersAsync / GetProblemDetailAsync - Track classification ----

    [Fact]
    public async Task GetProblemDetailAsync_UsesContestTrackColumn_NotOriginSourceGuessing()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = CreateRepository(context);
        context.Sites.Add(new DbSite { SiteId = 1, Name = "Site 1" });

        // OriginSource text mentions "OBI", but the real contest.Track column says GENERAL -
        // the column must win, not a text guess.
        var problem = new DbProblem { Title = "Problem A", Spj = "N", Defunct = "N", TimeLimit = 1000, MemoryLimit = 128, OriginSource = "OBI Regional 2023" };
        context.Problems.Add(problem);
        await context.SaveChangesAsync();
        context.ProblemSites.Add(new DbProblemSite { problemId = problem.ProblemId!.Value, SiteId = 1, IsActive = true });
        context.Contests.Add(new DbContest { ContestId = 1, Title = "Contest A", StartTime = DateTime.Now, EndTime = DateTime.Now.AddDays(1), Defunct = "N", Track = "GENERAL" });
        await context.SaveChangesAsync();
        context.ContestSites.Add(new DbContestSite { ContestId = 1, SiteId = 1 });
        context.ContestProblems.Add(new DbContestProblem { ContestId = 1, ProblemId = problem.ProblemId!.Value, Num = 1 });
        await context.SaveChangesAsync();

        PublicProblemDetailResponse detail = await repository.GetProblemDetailAsync(siteId: 1, problemId: problem.ProblemId!.Value);

        Assert.Equal(new[] { "GENERAL" }, detail.ContestTracks);
    }

    [Fact]
    public async Task GetProblemDetailAsync_ClassifiesByContestTrack_EvenWithoutMatchingOriginSourceText()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = CreateRepository(context);
        context.Sites.Add(new DbSite { SiteId = 1, Name = "Site 1" });

        // OriginSource has nothing to do with OBI - only the contest.Track column says so.
        var problem = new DbProblem { Title = "Problem A", Spj = "N", Defunct = "N", TimeLimit = 1000, MemoryLimit = 128, OriginSource = "Regional Qualifier" };
        context.Problems.Add(problem);
        await context.SaveChangesAsync();
        context.ProblemSites.Add(new DbProblemSite { problemId = problem.ProblemId!.Value, SiteId = 1, IsActive = true });
        context.Contests.Add(new DbContest { ContestId = 1, Title = "Contest A", StartTime = DateTime.Now, EndTime = DateTime.Now.AddDays(1), Defunct = "N", Track = "OBI" });
        await context.SaveChangesAsync();
        context.ContestSites.Add(new DbContestSite { ContestId = 1, SiteId = 1 });
        context.ContestProblems.Add(new DbContestProblem { ContestId = 1, ProblemId = problem.ProblemId!.Value, Num = 1 });
        await context.SaveChangesAsync();

        PublicProblemDetailResponse detail = await repository.GetProblemDetailAsync(siteId: 1, problemId: problem.ProblemId!.Value);

        Assert.Equal(new[] { "OBI" }, detail.ContestTracks);
    }

    [Fact]
    public async Task GetProblemDetailAsync_DefaultsToGeneral_WhenProblemHasNoContest()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = CreateRepository(context);
        int problemId = await SeedProblemAsync(context, siteId: 1);

        PublicProblemDetailResponse detail = await repository.GetProblemDetailAsync(siteId: 1, problemId: problemId);

        Assert.Equal(new[] { "GENERAL" }, detail.ContestTracks);
    }
}
