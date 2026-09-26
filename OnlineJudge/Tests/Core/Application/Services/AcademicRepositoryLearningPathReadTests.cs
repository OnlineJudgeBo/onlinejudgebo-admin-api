using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public class AcademicRepositoryLearningPathReadTests
{
    private static async Task<(AcademicRepository Repository, long StageId)> SeedAsync(JudgeSeed seed, string key = "algo101", string title = "Algoritmos", string language = "cpp", string category = "universidad")
    {
        var repository = new AcademicRepository(seed.Db, seed.Academic);
        await repository.CreateLearningPathAsync(1, new LearningPathAdminUpsertRequest { Key = key, Title = title, LanguagePrimary = language, Category = category, Description = "d" });
        var response = await repository.CreateLearningPathStageAsync(1, key, new LearningPathStageAdminRequest { Key = key + "_stage", Name = "Etapa 1", Order = 1 });
        var stageId = await seed.Academic.Topics.Where(topic => topic.TopicKey == key + "_stage").Select(topic => topic.TopicId).SingleAsync();
        return (repository, stageId);
    }

    [Fact]
    public async Task GetLearningPaths_SummarizesStagesProblemsAndAudienceForSite()
    {
        using var seed = new JudgeSeed();
        var active = seed.Problem("A");
        var (repository, stageId) = await SeedAsync(seed);
        await repository.CreateLearningPathTopicAsync(1, "algo101", stageId, new LearningPathTopicAdminRequest { Key = "t1", Title = "T1", Order = 1, ProblemIds = new List<int> { active } });
        await repository.CreateLearningPathAsync(2, new LearningPathAdminUpsertRequest { Key = "other", Title = "Otro sitio", LanguagePrimary = "py", Category = "x" });

        var paths = (await repository.GetLearningPathsAsync(1)).ToList();

        var path = Assert.Single(paths);
        Assert.Equal("algo101", path.Id);
        Assert.Equal(1, path.StageCount);
        Assert.Equal(1, path.EstimatedTotalProblems);
        Assert.Equal(new[] { "Estudiantes que practican C++", "Estudiantes universitarios", "Participantes de concursos de programación" }, path.TargetAudience);
        Assert.Empty(await repository.GetLearningPathsAsync(3));
    }

    [Theory]
    [InlineData("py", "colegio", "Estudiantes que practican Python", "Estudiantes de colegio")]
    [InlineData("java_script", "open-level", "Estudiantes que practican Java Script", "Open Level")]
    public async Task GetLearningPaths_DescribesAudience(string language, string category, string languageLine, string categoryLine)
    {
        using var seed = new JudgeSeed();
        await SeedAsync(seed, language: language, category: category);

        var audience = (await new AcademicRepository(seed.Db, seed.Academic).GetLearningPathsAsync(1)).Single().TargetAudience;

        Assert.Equal(new[] { languageLine, categoryLine, "Participantes de concursos de programación" }, audience);
    }

    [Fact]
    public async Task GetLearningPath_UnknownKeyOrSiteThrows()
    {
        using var seed = new JudgeSeed();
        var (repository, _) = await SeedAsync(seed);

        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetLearningPathAsync(1, "missing"));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetLearningPathAsync(2, "algo101"));
    }

    [Fact]
    public async Task LinkAndUnlinkStage_SharesStageBetweenPathsAndClearsProgress()
    {
        using var seed = new JudgeSeed();
        var problem = seed.Problem("A");
        var (repository, stageId) = await SeedAsync(seed);
        await repository.CreateLearningPathTopicAsync(1, "algo101", stageId, new LearningPathTopicAdminRequest { Key = "t1", Title = "T1", Order = 1, ProblemIds = new List<int> { problem } });
        await repository.CreateLearningPathAsync(1, new LearningPathAdminUpsertRequest { Key = "algo102", Title = "Algoritmos II", LanguagePrimary = "cpp", Category = "x" });

        var linked = await repository.LinkLearningPathStageAsync(1, "algo102", stageId);
        await repository.SaveLearningPathProgressAsync(1, "algo102", "ana", new LearningPathProgressUpdateRequest { CompletedTopicIds = new List<string> { "t1" } });

        Assert.NotNull(linked);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.LinkLearningPathStageAsync(1, "algo102", stageId));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.LinkLearningPathStageAsync(1, "algo102", 999));

        await repository.UnlinkLearningPathStageAsync(1, "algo102", stageId);

        Assert.Empty((await repository.GetLearningPathProgressAsync(1, "algo101", "ana")).CompletedTopicIds);
        Assert.Equal(1, (await repository.GetLearningPathsAsync(1)).Single(path => path.Id == "algo101").StageCount);
        Assert.Equal(0, (await repository.GetLearningPathsAsync(1)).Single(path => path.Id == "algo102").StageCount);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.UnlinkLearningPathStageAsync(1, "algo102", stageId));
    }
}
