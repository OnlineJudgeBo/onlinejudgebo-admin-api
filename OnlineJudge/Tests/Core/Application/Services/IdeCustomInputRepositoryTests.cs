using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
using static RepositoryTestSupport;

// CreateCustomInputRunAsync inserts a DbSolution then, in the same
// transaction, batch-inserts DbSourceCode/DbCustomInput/DbCustomInputCase
// rows that all reference the generated SolutionId — the same "parent then
// batched children" shape as the Problem FK bug. DbSolution.SolutionId is
// [DatabaseGenerated(Identity)], which sidesteps that specific defect, but
// these tests pin the FK integrity end to end against a real DbContext
// rather than assuming the attribute is enough.
public class IdeCustomInputRepositoryTests
{
    private static async Task<int> SeedValidTargetAsync(AppDbContext context, string userId, int siteId, int languageId)
    {
        // Sqlite enforces FKs for real (unlike InMemory), so DbProblemSite's
        // reference to DbSite needs an actual row to point at.
        context.Sites.Add(new DbSite { SiteId = siteId, Name = "Site " + siteId });
        context.Users.Add(new DbUser
        {
            UserId = userId,
            Ip = "127.0.0.1",
            SiteId = siteId,
            IsActive = true,
            IsDeleted = false
        });
        var problem = new DbProblem
        {
            Title = "Problem A",
            Spj = "N",
            Defunct = "N",
            TimeLimit = 1000,
            MemoryLimit = 128
        };
        context.Problems.Add(problem);
        await context.SaveChangesAsync();
        context.ProblemSites.Add(new DbProblemSite { problemId = problem.ProblemId!.Value, SiteId = siteId, IsActive = true });
        context.ProgrammingLanguages.Add(new DbProgrammingLanguage { LanguageId = languageId, Name = "C++" });
        await context.SaveChangesAsync();
        return problem.ProblemId!.Value;
    }

    private static IdeCustomInputRunCreation NewRun(string userId, int siteId, int problemId, int languageId, params IdeTestcaseRequest[] testcases) => new(
        UserId: userId,
        SiteId: siteId,
        ProblemId: problemId,
        LanguageId: languageId,
        ContestId: null,
        Num: 0,
        SourceCode: "print(1)",
        Testcases: testcases,
        Stdin: null,
        ClientIp: "127.0.0.1");

    [Fact]
    public async Task CreateCustomInputRunAsync_PersistsSolutionSourceAndCasesWithMatchingSolutionIdFk()
    {
        // Sqlite, not InMemory: CreateCustomInputRunAsync wraps everything
        // in a real transaction (BeginTransactionAsync), which the
        // InMemory provider does not support and throws on.
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = new IdeCustomInputRepository(context);
        int problemId = await SeedValidTargetAsync(context, "alice", siteId: 1, languageId: 54);

        int solutionId = await repository.CreateCustomInputRunAsync(NewRun("alice", 1, problemId, 54,
            new IdeTestcaseRequest { Input = "1 2", ExpectedOutput = "3" }));

        Assert.NotEqual(0, solutionId);
        DbSourceCode source = await context.SourceCodes.SingleAsync();
        Assert.Equal(solutionId, source.SolutionId);
        DbCustomInput customInput = await context.CustomInputs.SingleAsync();
        Assert.Equal(solutionId, customInput.SolutionId);
        DbCustomInputCase testCase = await context.CustomInputCases.SingleAsync();
        Assert.Equal(solutionId, testCase.SolutionId);
        Assert.Equal("1 2", testCase.InputText);
    }

    [Fact]
    public async Task CreateCustomInputRunAsync_UsesStdinAsSingleCase_WhenNoTestcasesProvided()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = new IdeCustomInputRepository(context);
        int problemId = await SeedValidTargetAsync(context, "alice", siteId: 1, languageId: 54);

        var run = NewRun("alice", 1, problemId, 54) with { Stdin = "stdin value" };
        await repository.CreateCustomInputRunAsync(run);

        DbCustomInputCase testCase = await context.CustomInputCases.SingleAsync();
        Assert.Equal(1, testCase.CaseNumber);
        Assert.Equal("stdin value", testCase.InputText);
    }

    [Fact]
    public async Task CreateCustomInputRunAsync_ThrowsWhenUserInvalidForSite()
    {
        using AppDbContext context = CreateContext();
        var repository = new IdeCustomInputRepository(context);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.CreateCustomInputRunAsync(NewRun("nobody", 1, 1, 54)));
    }

    [Fact]
    public async Task CreateCustomInputRunAsync_ThrowsWhenProblemNotAvailableForSite()
    {
        using AppDbContext context = CreateContext();
        var repository = new IdeCustomInputRepository(context);
        context.Users.Add(new DbUser { UserId = "alice", Ip = "127.0.0.1", SiteId = 1, IsActive = true, IsDeleted = false });
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.CreateCustomInputRunAsync(NewRun("alice", 1, problemId: 9999, languageId: 54)));
    }

    [Fact]
    public async Task CreateCustomInputRunAsync_ThrowsWhenLanguageUnsupported()
    {
        using AppDbContext context = CreateContext();
        var repository = new IdeCustomInputRepository(context);
        context.Users.Add(new DbUser { UserId = "alice", Ip = "127.0.0.1", SiteId = 1, IsActive = true, IsDeleted = false });
        var problem = new DbProblem { Title = "Problem A", Spj = "N", Defunct = "N", TimeLimit = 1000, MemoryLimit = 128 };
        context.Problems.Add(problem);
        await context.SaveChangesAsync();
        context.ProblemSites.Add(new DbProblemSite { problemId = problem.ProblemId!.Value, SiteId = 1, IsActive = true });
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.CreateCustomInputRunAsync(NewRun("alice", 1, problem.ProblemId!.Value, languageId: 9999)));
    }

    [Fact]
    public async Task IsCustomInputAsync_ReturnsTrueOnlyForCustomInputSolutions()
    {
        using SqliteContext<AppDbContext> scope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        AppDbContext context = scope.Context;
        var repository = new IdeCustomInputRepository(context);
        int problemId = await SeedValidTargetAsync(context, "alice", siteId: 1, languageId: 54);
        int solutionId = await repository.CreateCustomInputRunAsync(NewRun("alice", 1, problemId, 54));

        Assert.True(await repository.IsCustomInputAsync(solutionId));
        Assert.False(await repository.IsCustomInputAsync(solutionId + 1000));
    }
}
