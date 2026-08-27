using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
using static RepositoryTestSupport;

// CreateContestAsync has the same shape as the ProblemRepository FK bug
// (insert a parent, then batch-insert several child collections that
// reference the parent's generated id: ContestProblems, ContestUsers,
// ContestSites), though DbContest.ContestId is a non-nullable int, which
// EF's value-generation convention treats as "unset" correctly at its CLR
// default (0) - so it isn't exposed to the same failure mode. These tests
// exist to pin that down against a real AppDbContext (EF InMemory), not a
// mocked IContestsRepository, so any future change to the key type or the
// insert ordering is caught the same way the Problem bug would have been.
public class ContestsRepositoryTests
{
    private static Contest NewContest(string title) => new()
    {
        Title = title,
        StartTime = new DateTime(2026, 1, 1),
        EndTime = new DateTime(2026, 1, 2),
        Defunct = "N",
        Private = 0,
        Langmask = 0,
        Track = "GENERAL",
        Level = "PRACTICE",
        ContestProblems = new List<ContestProblem>(),
        ContestUsers = new List<ContestUser>(),
        ProgrammingLanguages = new List<ProgrammingLanguage>()
    };

    [Fact]
    public async Task CreateContestAsync_AssignsGeneratedId_AndCreatesActiveContestSite()
    {
        using AppDbContext context = CreateContext();
        var repository = new ContestsRepository(context, CreateMapper());

        Contest created = await repository.CreateContestAsync(NewContest("Contest A"), siteId: 5);

        Assert.NotEqual(0, created.ContestId);

        DbContestSite site = await context.ContestSites.SingleAsync();
        Assert.Equal(created.ContestId, site.ContestId);
        Assert.Equal(5, site.SiteId);
    }

    [Fact]
    public async Task CreateContestAsync_CalledTwice_ProducesDistinctIds()
    {
        using AppDbContext context = CreateContext();
        var repository = new ContestsRepository(context, CreateMapper());

        Contest first = await repository.CreateContestAsync(NewContest("Contest A"), siteId: 1);
        Contest second = await repository.CreateContestAsync(NewContest("Contest B"), siteId: 1);

        Assert.NotEqual(first.ContestId, second.ContestId);
        Assert.Equal(2, await context.Contests.CountAsync());
    }

    [Fact]
    public async Task CreateContestAsync_PersistsContestProblemsWithMatchingContestIdFk()
    {
        using AppDbContext context = CreateContext();
        var repository = new ContestsRepository(context, CreateMapper());

        // Seed the referenced problem so ContestProblems.ProblemId is real.
        var problemRepository = new ProblemRepository(context, CreateMapper());
        Problem problem = await problemRepository.CreateProblemAsync(new Problem
        {
            ProblemId = 0,
            Title = "Problem A",
            Spj = "N",
            Defunct = "N",
            TimeLimit = 1000,
            MemoryLimit = 128,
            SampleCases = new List<ProblemSample>()
        }, siteId: 1);

        Contest toCreate = NewContest("Contest A");
        toCreate.ContestProblems.Add(new ContestProblem { ProblemId = problem.ProblemId, Num = 1 });

        Contest created = await repository.CreateContestAsync(toCreate, siteId: 1);

        DbContestProblem contestProblem = await context.ContestProblems.SingleAsync();
        Assert.Equal(created.ContestId, contestProblem.ContestId);
        Assert.Equal(problem.ProblemId, contestProblem.ProblemId);
    }

    [Fact]
    public async Task CreateContestAsync_SkipsProgrammingLanguagesThatDoNotExist()
    {
        using AppDbContext context = CreateContext();
        var repository = new ContestsRepository(context, CreateMapper());

        Contest toCreate = NewContest("Contest A");
        toCreate.ProgrammingLanguages!.Add(new ProgrammingLanguage { LanguageId = 999 });

        Contest created = await repository.CreateContestAsync(toCreate, siteId: 1);

        Assert.Empty(created.ProgrammingLanguages ?? Array.Empty<ProgrammingLanguage>());
    }

    [Fact]
    public async Task UpdateContestAsync_ThrowsWhenContestNotFoundForSite()
    {
        using AppDbContext context = CreateContext();
        var repository = new ContestsRepository(context, CreateMapper());

        Contest created = await repository.CreateContestAsync(NewContest("Contest A"), siteId: 1);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            repository.UpdateContestAsync(created.ContestId, NewContest("Updated"), siteId: 2));
    }

    [Fact]
    public async Task PromoteContestAsync_SetsDefunctToO()
    {
        using AppDbContext context = CreateContext();
        var repository = new ContestsRepository(context, CreateMapper());

        Contest created = await repository.CreateContestAsync(NewContest("Contest A"), siteId: 1);

        await repository.PromoteContestAsync(created.ContestId, siteId: 1);

        Assert.Equal("O", (await context.Contests.SingleAsync()).Defunct);
    }

    [Fact]
    public async Task PromoteContestAsync_ThrowsWhenNotFoundForSite()
    {
        using AppDbContext context = CreateContext();
        var repository = new ContestsRepository(context, CreateMapper());

        Contest created = await repository.CreateContestAsync(NewContest("Contest A"), siteId: 1);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            repository.PromoteContestAsync(created.ContestId, siteId: 2));
    }
}
