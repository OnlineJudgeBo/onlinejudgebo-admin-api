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
}
