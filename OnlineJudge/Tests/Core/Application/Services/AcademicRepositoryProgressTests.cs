using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

public class AcademicRepositoryProgressTests
{
    private static async Task<AcademicRepository> SeedPathAsync(JudgeSeed seed)
    {
        var repository = new AcademicRepository(seed.Db, seed.Academic);
        var problemId = seed.Problem("Suma");
        await repository.CreateLearningPathAsync(1, new LearningPathAdminUpsertRequest { Key = "algo101", Title = "Algorithms", LanguagePrimary = "cpp", Category = "core" });
        var path = await seed.Academic.LearningPaths.SingleAsync();
        var stage = new DbAcademicTopic { TopicKey = "basics", Name = "Basics", SortOrder = 1 };
        seed.Academic.Topics.Add(stage);
        await seed.Academic.SaveChangesAsync();
        seed.Academic.LearningPathTopics.Add(new DbLearningPathTopic { LearningPathId = path.LearningPathId, TopicId = stage.TopicId });
        await seed.Academic.SaveChangesAsync();
        foreach (var (key, order) in new[] { ("variables", 1), ("loops", 2), ("arrays", 3) })
        {
            await repository.CreateLearningPathTopicAsync(1, "algo101", stage.TopicId, new LearningPathTopicAdminRequest { Key = key, Title = key, Order = order, ProblemIds = new List<int> { problemId } });
        }

        return repository;
    }

    [Fact]
    public async Task GetProgress_EmptyForNewUser()
    {
        using var seed = new JudgeSeed();
        var repository = await SeedPathAsync(seed);

        var progress = await repository.GetLearningPathProgressAsync(1, "algo101", "ana");

        Assert.Equal("algo101", progress.LearningPathKey);
        Assert.Null(progress.LastTopicId);
        Assert.Empty(progress.CompletedTopicIds);
        Assert.Null(progress.UpdatedAtUtc);
    }

    [Fact]
    public async Task SaveProgress_NormalizesCaseOrderAndDropsUnknownTopics()
    {
        using var seed = new JudgeSeed();
        var repository = await SeedPathAsync(seed);

        var saved = await repository.SaveLearningPathProgressAsync(1, "algo101", "ana", new LearningPathProgressUpdateRequest
        {
            LastTopicId = " LOOPS ",
            CompletedTopicIds = new List<string> { "arrays", "VARIABLES", "ghost", " ", "arrays" }
        });
        var reloaded = await repository.GetLearningPathProgressAsync(1, "algo101", "ana");

        Assert.Equal("loops", saved.LastTopicId);
        Assert.Equal(new[] { "variables", "arrays" }, saved.CompletedTopicIds);
        Assert.Equal(saved.CompletedTopicIds, reloaded.CompletedTopicIds);
        Assert.Equal("loops", reloaded.LastTopicId);
        Assert.NotNull(reloaded.UpdatedAtUtc);
    }

    [Fact]
    public async Task SaveProgress_ReplacesCompletedSetAndKeepsOtherUsers()
    {
        using var seed = new JudgeSeed();
        var repository = await SeedPathAsync(seed);
        await repository.SaveLearningPathProgressAsync(1, "algo101", "ana", new LearningPathProgressUpdateRequest { CompletedTopicIds = new List<string> { "variables", "loops" } });
        await repository.SaveLearningPathProgressAsync(1, "algo101", "bob", new LearningPathProgressUpdateRequest { CompletedTopicIds = new List<string> { "variables" } });

        await repository.SaveLearningPathProgressAsync(1, "algo101", "ana", new LearningPathProgressUpdateRequest { LastTopicId = "unknown", CompletedTopicIds = new List<string> { "arrays" } });

        var ana = await repository.GetLearningPathProgressAsync(1, "algo101", "ana");
        var bob = await repository.GetLearningPathProgressAsync(1, "algo101", "bob");
        Assert.Equal(new[] { "arrays" }, ana.CompletedTopicIds);
        Assert.Null(ana.LastTopicId);
        Assert.Equal(new[] { "variables" }, bob.CompletedTopicIds);
        Assert.Single(await seed.Academic.LearningPathProgresses.Where(item => item.UserId == "ana").ToListAsync());
    }

    [Fact]
    public async Task Progress_UnknownPathOrOtherSiteOrPathWithoutStages()
    {
        using var seed = new JudgeSeed();
        var repository = await SeedPathAsync(seed);
        await repository.CreateLearningPathAsync(1, new LearningPathAdminUpsertRequest { Key = "empty", Title = "Empty", LanguagePrimary = "cpp", Category = "core" });

        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetLearningPathProgressAsync(1, "missing", "ana"));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetLearningPathProgressAsync(2, "algo101", "ana"));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveLearningPathProgressAsync(1, "empty", "ana", new LearningPathProgressUpdateRequest()));
        Assert.Equal("La ruta de aprendizaje no tiene etapas configuradas.", error.Message);
    }

    [Fact]
    public async Task Progress_CompletesTopicWhenEveryCoreProblemIsAccepted()
    {
        using var seed = new JudgeSeed();
        var repository = new AcademicRepository(seed.Db, seed.Academic);
        var core = seed.Problem("Core");
        var challenge = seed.Problem("Challenge");
        var other = seed.Problem("Other");
        seed.Save();
        await repository.CreateLearningPathAsync(1, new LearningPathAdminUpsertRequest { Key = "icpc", Title = "ICPC" });
        var path = await repository.CreateLearningPathStageAsync(1, "icpc", new LearningPathStageAdminRequest { Key = "icpc_basics", Name = "Basics", Order = 1 });
        var stageId = path.Stages.Single().StageId;
        await repository.CreateLearningPathTopicAsync(1, "icpc", stageId, new LearningPathTopicAdminRequest { Key = "io", Title = "IO", Order = 1, ProblemIds = new List<int> { core }, ChallengeProblemIds = new List<int> { challenge } });
        await repository.CreateLearningPathTopicAsync(1, "icpc", stageId, new LearningPathTopicAdminRequest { Key = "theory", Title = "Theory", Order = 2 });
        seed.Solution("ana", challenge, 4);
        seed.Solution("ana", other, 4);
        seed.Solution("ana", core, 6);
        seed.Solution("ana", core, 4, siteId: 2);

        var before = await repository.GetLearningPathProgressAsync(1, "icpc", "ana");
        seed.Solution("ana", core, 4);
        var after = await repository.GetLearningPathProgressAsync(1, "icpc", "ana");

        Assert.Empty(before.CompletedTopicIds);
        Assert.Equal(new[] { challenge }, before.SolvedProblemIds);
        Assert.Equal(new[] { "io" }, after.CompletedTopicIds);
        Assert.Equal(new[] { core, challenge }.OrderBy(id => id), after.SolvedProblemIds);
        var topic = (await repository.GetLearningPathAsync(1, "icpc")).Stages.Single().Topics.First();
        Assert.Equal(new[] { core }, topic.RecommendedProblems);
        Assert.Equal(new[] { challenge }, topic.ChallengeProblems);
    }

    [Fact]
    public async Task Sites_ToggleVisibilityPerSite()
    {
        using var seed = new JudgeSeed();
        seed.Site(1).Site(2).Save();
        var repository = new AcademicRepository(seed.Db, seed.Academic);
        var shared = seed.Problem("Shared");
        seed.Db.ProblemSites.Add(new DbProblemSite { problemId = shared, SiteId = 2, IsActive = true });
        var onlyOwner = seed.Problem("Only owner");
        seed.Save();
        await repository.CreateLearningPathAsync(1, new LearningPathAdminUpsertRequest { Key = "icpc", Title = "ICPC" });
        var path = await repository.CreateLearningPathStageAsync(1, "icpc", new LearningPathStageAdminRequest { Key = "icpc_basics", Name = "Basics", Order = 1 });
        await repository.CreateLearningPathTopicAsync(1, "icpc", path.Stages.Single().StageId, new LearningPathTopicAdminRequest { Key = "io", Title = "IO", Order = 1, ProblemIds = new List<int> { shared, onlyOwner } });

        var sites = (await repository.GetLearningPathSitesAsync(1, "icpc")).ToList();
        Assert.Equal(new[] { (1, true, true, 0), (2, false, false, 1) }, sites.Select(site => (site.SiteId, site.IsOwner, site.IsActive, site.MissingProblemCount)));
        Assert.Empty(await repository.GetLearningPathsAsync(2));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetLearningPathAsync(2, "icpc"));

        await repository.SetLearningPathSiteAsync(1, "icpc", 2, true);
        await repository.SetLearningPathSiteAsync(1, "icpc", 1, false);

        Assert.Single(await repository.GetLearningPathsAsync(2));
        Assert.Equal(new[] { shared }, (await repository.GetLearningPathAsync(2, "icpc")).Stages.Single().Topics.Single().RecommendedProblems);
        Assert.Empty(await repository.GetLearningPathsAsync(1));
        Assert.Single(await repository.GetManagedLearningPathsAsync(1));
        Assert.Empty(await repository.GetManagedLearningPathsAsync(2));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateLearningPathAsync(2, new LearningPathAdminUpsertRequest { Key = "icpc", Title = "Copy" }));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SetLearningPathSiteAsync(1, "icpc", 99, true));
    }
}
