using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
using static RepositoryTestSupport;

// Regression coverage for the FK bug where creating a problem sent
// problem_id=0 (the CLR default of ProblemForCreation.ProblemId, an int,
// not the int? default of DbProblem.ProblemId) into the insert, so EF
// treated it as an explicit value instead of a store-generated one. Without
// the `dbProblem.ProblemId = null` fix in ProblemRepository, every Create
// test below fails: EF's own value-generation bookkeeping never resolves a
// real key for the problem row, so the follow-up ProblemSites insert throws
// (InMemory) or the sample-case insert references an id that was never
// actually persisted (MySQL's original FK violation). Uses the EF InMemory
// provider so the same ValueGenerationManager logic that caused the bug
// runs for real, without needing a live MySQL instance.
//
// The rest of the file exercises every other public method of
// ProblemRepository so the whole module (not just Create) has a real,
// DB-backed regression net.
public class ProblemRepositoryTests
{
    private static Problem NewProblem(string title, string defunct = "N") => new()
    {
        // ProblemId = 0, not left as the domain model's own null default:
        // ProblemForCreation.ProblemId is a non-nullable int, so every real
        // create request maps in a literal 0 here, which is the exact
        // precondition that caused the FK bug.
        ProblemId = 0,
        Title = title,
        Spj = "N",
        Defunct = defunct,
        TimeLimit = 1000,
        MemoryLimit = 128,
        SampleCases = new List<ProblemSample>
        {
            new() { Num = 1, Input = "in", Output = "out" }
        }
    };

    // ---- Model-wide guard --------------------------------------------
    // The FK bug only exists because DbProblem.ProblemId is a nullable,
    // single-column, store-generated PK: EF's "is this value still the CLR
    // default?" check only works for non-nullable numeric keys, since 0 (a
    // very common accidental value from a mapped DTO) is indistinguishable
    // from "not set" for those, but not for `int?` unless it's actually
    // null. If a future entity repeats that shape AND gets a Create path
    // that inserts brand-new rows, that path needs the same `Id = null;`
    // reset that ProblemRepository.CreateProblemAsync has. This test
    // enumerates the whole EF model so that shape doesn't slip in
    // unnoticed. DbProgrammingLanguage also has it, but no repository
    // creates new rows there today (ContestsRepository only attaches
    // existing ones via FindAsync) — if that ever changes, audit it the
    // same way ProblemRepository was fixed before removing it from the
    // allow-list below.
    [Fact]
    public void Model_NullableStoreGeneratedPrimaryKeys_AreOnlyTheKnownAllowedCases()
    {
        using AppDbContext context = CreateContext();

        List<string> nullablePkEntities = context.Model.GetEntityTypes()
            .Where(entityType =>
            {
                IKey? pk = entityType.FindPrimaryKey();
                return pk is { Properties.Count: 1 }
                    && pk.Properties[0].ValueGenerated == ValueGenerated.OnAdd
                    && Nullable.GetUnderlyingType(pk.Properties[0].ClrType) != null;
            })
            .Select(entityType => entityType.ClrType.Name)
            .OrderBy(name => name)
            .ToList();

        Assert.Equal(new[] { nameof(DbProblem), nameof(DbProgrammingLanguage) }, nullablePkEntities);
    }

    // ---- CreateProblemAsync --------------------------------------------

    [Fact]
    public async Task CreateProblemAsync_AssignsGeneratedId_AndPersistsSampleCasesWithMatchingFk()
    {
        using AppDbContext context = CreateContext();
        var repository = new ProblemRepository(context, CreateMapper());

        Problem created = await repository.CreateProblemAsync(NewProblem("Problem A"), siteId: 1);

        Assert.NotNull(created.ProblemId);
        Assert.NotEqual(0, created.ProblemId!.Value);

        DbProblemSample sample = await context.ProblemSampleCases.SingleAsync();
        Assert.Equal(created.ProblemId.Value, sample.ProblemId);
    }

    [Fact]
    public async Task CreateProblemAsync_CalledTwice_ProducesDistinctIdsAndNoOrphanSampleCases()
    {
        using AppDbContext context = CreateContext();
        var repository = new ProblemRepository(context, CreateMapper());

        Problem first = await repository.CreateProblemAsync(NewProblem("Problem A"), siteId: 1);
        Problem second = await repository.CreateProblemAsync(NewProblem("Problem B"), siteId: 1);

        Assert.NotEqual(first.ProblemId, second.ProblemId);

        List<int> problemIds = await context.Problems.Select(p => p.ProblemId!.Value).ToListAsync();
        List<int> sampleCaseProblemIds = await context.ProblemSampleCases.Select(s => s.ProblemId).ToListAsync();
        Assert.Equal(problemIds.OrderBy(id => id), sampleCaseProblemIds.OrderBy(id => id));
    }

    [Fact]
    public async Task CreateProblemAsync_CreatesActiveProblemSiteForRequestedSite()
    {
        using AppDbContext context = CreateContext();
        var repository = new ProblemRepository(context, CreateMapper());

        Problem created = await repository.CreateProblemAsync(NewProblem("Problem A"), siteId: 7);

        DbProblemSite site = await context.ProblemSites.SingleAsync();
        Assert.Equal(created.ProblemId!.Value, site.problemId);
        Assert.Equal(7, site.SiteId);
        Assert.True(site.IsActive);
    }

    // ---- UpdateProblemAsync --------------------------------------------

    [Fact]
    public async Task UpdateProblemAsync_ReplacesSampleCases_KeepingProblemIdFkConsistent()
    {
        using AppDbContext context = CreateContext();
        var repository = new ProblemRepository(context, CreateMapper());

        Problem created = await repository.CreateProblemAsync(NewProblem("Problem A"), siteId: 1);
        int problemId = created.ProblemId!.Value;

        var update = new Problem
        {
            Title = "Problem A updated",
            Spj = "N",
            Defunct = "N",
            TimeLimit = 2000,
            MemoryLimit = 256,
            SampleCases = new List<ProblemSample>
            {
                new() { Num = 1, Input = "in2", Output = "out2" }
            }
        };

        await repository.UpdateProblemAsync("user1", problemId, update, siteId: 1);

        DbProblemSample sample = await context.ProblemSampleCases.SingleAsync();
        Assert.Equal(problemId, sample.ProblemId);
        Assert.Equal("in2", sample.Input);
    }

    [Fact]
    public async Task UpdateProblemAsync_ThrowsWhenProblemNotFoundForSite()
    {
        using AppDbContext context = CreateContext();
        var repository = new ProblemRepository(context, CreateMapper());

        Problem created = await repository.CreateProblemAsync(NewProblem("Problem A"), siteId: 1);

        var update = new Problem { Title = "x", Spj = "N", SampleCases = new List<ProblemSample>() };

        // Right problem id, wrong site: must not be treated as found.
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            repository.UpdateProblemAsync("user1", created.ProblemId!.Value, update, siteId: 2));
    }

    [Fact]
    public async Task UpdateProblemAsync_KeepsExistingDefunctWhenUpdateValueIsBlank()
    {
        using AppDbContext context = CreateContext();
        var repository = new ProblemRepository(context, CreateMapper());

        Problem created = await repository.CreateProblemAsync(NewProblem("Problem A", defunct: "Y"), siteId: 1);

        var update = new Problem
        {
            Title = "Problem A updated",
            Spj = "N",
            Defunct = "   ",
            SampleCases = new List<ProblemSample>()
        };

        Problem updated = await repository.UpdateProblemAsync("user1", created.ProblemId!.Value, update, siteId: 1);

        Assert.Equal("Y", updated.Defunct);
    }

    // ---- GetProblemByIdAsync --------------------------------------------

    [Fact]
    public async Task GetProblemByIdAsync_ReturnsSampleCasesOrderedByNum()
    {
        using AppDbContext context = CreateContext();
        var mapper = CreateMapper();
        var repository = new ProblemRepository(context, mapper);

        Problem toCreate = NewProblem("Problem A");
        toCreate.SampleCases = new List<ProblemSample>
        {
            new() { Num = 2, Input = "second", Output = "2" },
            new() { Num = 1, Input = "first", Output = "1" }
        };
        Problem created = await repository.CreateProblemAsync(toCreate, siteId: 1);

        Problem fetched = await repository.GetProblemByIdAsync(created.ProblemId!.Value, siteId: 1);

        Assert.Equal(new[] { "first", "second" }, fetched.SampleCases.Select(s => s.Input));
    }

    [Fact]
    public async Task GetProblemByIdAsync_ReturnsNullWhenSiteDoesNotMatch()
    {
        using AppDbContext context = CreateContext();
        var repository = new ProblemRepository(context, CreateMapper());

        Problem created = await repository.CreateProblemAsync(NewProblem("Problem A"), siteId: 1);

        Problem fetched = await repository.GetProblemByIdAsync(created.ProblemId!.Value, siteId: 2);

        Assert.Null(fetched);
    }

    // ---- GetAllProblemsAsync / GetAllProblemsForAdminAsync --------------

    [Fact]
    public async Task GetAllProblemsAsync_ExcludesArchivedProblemsAndOtherSites()
    {
        using AppDbContext context = CreateContext();
        var repository = new ProblemRepository(context, CreateMapper());

        await repository.CreateProblemAsync(NewProblem("Active", defunct: "N"), siteId: 1);
        await repository.CreateProblemAsync(NewProblem("Hidden", defunct: "Y"), siteId: 1);
        await repository.CreateProblemAsync(NewProblem("Archived", defunct: "O"), siteId: 1);
        await repository.CreateProblemAsync(NewProblem("OtherSite", defunct: "N"), siteId: 2);

        IEnumerable<Problem> result = await repository.GetAllProblemsAsync(siteId: 1);

        Assert.Equal(new[] { "Hidden", "Active" }, result.Select(p => p.Title));
    }

    [Fact]
    public async Task GetAllProblemsForAdminAsync_IncludesArchivedProblems()
    {
        using AppDbContext context = CreateContext();
        var repository = new ProblemRepository(context, CreateMapper());

        await repository.CreateProblemAsync(NewProblem("Active", defunct: "N"), siteId: 1);
        await repository.CreateProblemAsync(NewProblem("Archived", defunct: "O"), siteId: 1);

        IEnumerable<Problem> result = await repository.GetAllProblemsForAdminAsync(siteId: 1);

        Assert.Equal(new[] { "Archived", "Active" }, result.Select(p => p.Title));
    }

    // ---- SearchProblemAsync / SearchProblemForAdminAsync ----------------

    [Fact]
    public async Task SearchProblemForAdminAsync_FiltersByTitle_AcrossAllDefunctStates()
    {
        using AppDbContext context = CreateContext();
        var repository = new ProblemRepository(context, CreateMapper());

        await repository.CreateProblemAsync(NewProblem("Dynamic Programming", defunct: "O"), siteId: 1);
        await repository.CreateProblemAsync(NewProblem("Greedy", defunct: "N"), siteId: 1);

        IEnumerable<Problem> result = await repository.SearchProblemForAdminAsync("Dynamic", siteId: 1);

        Assert.Equal(new[] { "Dynamic Programming" }, result.Select(p => p.Title));
    }

    [Fact]
    public async Task SearchProblemAsync_ExcludesArchivedEvenWhenTitleMatches()
    {
        using AppDbContext context = CreateContext();
        var repository = new ProblemRepository(context, CreateMapper());

        await repository.CreateProblemAsync(NewProblem("Dynamic Programming", defunct: "O"), siteId: 1);

        IEnumerable<Problem> result = await repository.SearchProblemAsync("Dynamic", siteId: 1);

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchProblemAsync_MatchesByProblemId()
    {
        using AppDbContext context = CreateContext();
        var repository = new ProblemRepository(context, CreateMapper());

        Problem created = await repository.CreateProblemAsync(NewProblem("Problem A"), siteId: 1);

        IEnumerable<Problem> result = await repository.SearchProblemAsync(created.ProblemId!.Value.ToString(), siteId: 1);

        Assert.Equal(new[] { created.ProblemId }, result.Select(p => p.ProblemId));
    }

    // ---- PromoteProblemAsync --------------------------------------------

    [Fact]
    public async Task PromoteProblemAsync_SetsDefunctToOForGivenIds()
    {
        using AppDbContext context = CreateContext();
        var repository = new ProblemRepository(context, CreateMapper());

        Problem first = await repository.CreateProblemAsync(NewProblem("Problem A"), siteId: 1);
        Problem second = await repository.CreateProblemAsync(NewProblem("Problem B"), siteId: 1);
        Problem untouched = await repository.CreateProblemAsync(NewProblem("Problem C"), siteId: 1);

        await repository.PromoteProblemAsync(new List<int> { first.ProblemId!.Value, second.ProblemId!.Value });

        List<DbProblem> problems = await context.Problems.ToListAsync();
        Assert.Equal("O", problems.Single(p => p.ProblemId == first.ProblemId).Defunct);
        Assert.Equal("O", problems.Single(p => p.ProblemId == second.ProblemId).Defunct);
        Assert.Equal("N", problems.Single(p => p.ProblemId == untouched.ProblemId).Defunct);
    }

    [Fact]
    public async Task PromoteProblemAsync_ThrowsWhenNoProblemsMatch()
    {
        using AppDbContext context = CreateContext();
        var repository = new ProblemRepository(context, CreateMapper());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            repository.PromoteProblemAsync(new List<int> { 9999 }));
    }

    // ---- ChangeProblemVisibilityAsync ------------------------------------

    [Fact]
    public async Task ChangeProblemVisibilityAsync_TogglesBetweenNAndY()
    {
        using AppDbContext context = CreateContext();
        var repository = new ProblemRepository(context, CreateMapper());

        Problem created = await repository.CreateProblemAsync(NewProblem("Problem A", defunct: "N"), siteId: 1);
        int problemId = created.ProblemId!.Value;

        await repository.ChangeProblemVisibilityAsync(problemId, siteId: 1);
        Assert.Equal("Y", (await context.Problems.SingleAsync(p => p.ProblemId == problemId)).Defunct);

        await repository.ChangeProblemVisibilityAsync(problemId, siteId: 1);
        Assert.Equal("N", (await context.Problems.SingleAsync(p => p.ProblemId == problemId)).Defunct);
    }

    [Fact]
    public async Task ChangeProblemVisibilityAsync_DoesNotChangeArchivedProblems()
    {
        using AppDbContext context = CreateContext();
        var repository = new ProblemRepository(context, CreateMapper());

        Problem created = await repository.CreateProblemAsync(NewProblem("Problem A", defunct: "O"), siteId: 1);
        int problemId = created.ProblemId!.Value;

        await repository.ChangeProblemVisibilityAsync(problemId, siteId: 1);

        Assert.Equal("O", (await context.Problems.SingleAsync(p => p.ProblemId == problemId)).Defunct);
    }

    [Fact]
    public async Task ChangeProblemVisibilityAsync_ThrowsWhenNotFoundForSite()
    {
        using AppDbContext context = CreateContext();
        var repository = new ProblemRepository(context, CreateMapper());

        Problem created = await repository.CreateProblemAsync(NewProblem("Problem A"), siteId: 1);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            repository.ChangeProblemVisibilityAsync(created.ProblemId!.Value, siteId: 2));
    }

    // ---- DeleteProblemAsync --------------------------------------------

    [Fact]
    public async Task DeleteProblemAsync_DeactivatesProblemSite_AndExcludesFromGetAll()
    {
        using AppDbContext context = CreateContext();
        var repository = new ProblemRepository(context, CreateMapper());

        Problem created = await repository.CreateProblemAsync(NewProblem("Problem A"), siteId: 1);

        await repository.DeleteProblemAsync(created.ProblemId!.Value, siteId: 1);

        DbProblemSite site = await context.ProblemSites.SingleAsync();
        Assert.False(site.IsActive);
        Assert.Empty(await repository.GetAllProblemsAsync(siteId: 1));
    }

    [Fact]
    public async Task DeleteProblemAsync_IsNoOpWhenProblemSiteNotFound()
    {
        using AppDbContext context = CreateContext();
        var repository = new ProblemRepository(context, CreateMapper());

        // Must not throw even though nothing was ever created.
        await repository.DeleteProblemAsync(9999, siteId: 1);

        Assert.Empty(await context.ProblemSites.ToListAsync());
    }
}
