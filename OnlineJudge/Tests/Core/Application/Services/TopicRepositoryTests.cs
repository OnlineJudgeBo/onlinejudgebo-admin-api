using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
using static RepositoryTestSupport;

public class TopicRepositoryTests
{
    private static async Task<DbTopic> SeedTopicAsync(AppDbContext context, string name)
    {
        var topic = new DbTopic { Name = name };
        context.Topics.Add(topic);
        await context.SaveChangesAsync();
        return topic;
    }

    private static async Task<DbProblem> SeedProblemAsync(AppDbContext context)
    {
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
        return problem;
    }

    [Fact]
    public async Task CreateTopic_PersistsTopicByName()
    {
        using AppDbContext context = CreateContext();
        var repository = new TopicRepository(context, CreateMapper());

        await repository.CreateTopic(new Topic { Name = "Graphs" });

        DbTopic topic = await context.Topics.SingleAsync();
        Assert.Equal("Graphs", topic.Name);
    }

    [Fact]
    public async Task AddClassificationToTopic_CreatesAndAttachesNewClassifications()
    {
        using AppDbContext context = CreateContext();
        var repository = new TopicRepository(context, CreateMapper());
        DbTopic topic = await SeedTopicAsync(context, "Graphs");

        await repository.AddClassificationToTopic(topic.TopicId, new Topic
        {
            Name = "Graphs",
            Classifications = new List<Classification>
            {
                new() { Name = "BFS" },
                new() { Name = "DFS" }
            }
        });

        DbTopic reloaded = await context.Topics
            .Include(t => t.Classifications)
            .SingleAsync(t => t.TopicId == topic.TopicId);
        Assert.Equal(new[] { "BFS", "DFS" }, reloaded.Classifications.Select(c => c.Name).OrderBy(n => n));
        Assert.All(reloaded.Classifications, c => Assert.Equal(topic.TopicId, c.TopicId));
    }

    // The DTO this method is wired to (ClassificationAddToTopic) never
    // carries an id, so in the app today every Classification here has
    // ClassificationId == 0 and the "create new" path above is what
    // actually runs. This test locks down the other half of the contract:
    // if a caller ever does pass an existing id (an "attach existing
    // classification" use), AddClassificationToTopic must look it up and
    // attach the real row instead of mapping a second, conflicting
    // instance of it.
    [Fact]
    public async Task AddClassificationToTopic_AttachesExistingClassification_WhenClassificationIdAlreadyExists()
    {
        using AppDbContext context = CreateContext();
        var repository = new TopicRepository(context, CreateMapper());
        DbTopic topic = await SeedTopicAsync(context, "Graphs");
        context.Classifications.Add(new DbClassification { TopicId = topic.TopicId, Name = "BFS" });
        await context.SaveChangesAsync();
        int existingClassificationId = (await context.Classifications.SingleAsync()).ClassificationId;

        await repository.AddClassificationToTopic(topic.TopicId, new Topic
        {
            Name = "Graphs",
            Classifications = new List<Classification>
            {
                new() { ClassificationId = existingClassificationId, Name = "BFS (renamed)" }
            }
        });

        DbClassification classification = await context.Classifications.SingleAsync();
        Assert.Equal(existingClassificationId, classification.ClassificationId);
        Assert.Equal(topic.TopicId, classification.TopicId);
        // Attaching must not rename the existing row from whatever Name it
        // already has - the request's Name is only used for brand-new rows.
        Assert.Equal("BFS", classification.Name);
    }

    [Fact]
    public async Task AddClassificationsToProblemAsync_AttachesExistingClassificationsToProblem()
    {
        using AppDbContext context = CreateContext();
        var repository = new TopicRepository(context, CreateMapper());
        DbProblem problem = await SeedProblemAsync(context);
        DbTopic topic = await SeedTopicAsync(context, "Graphs");
        context.Classifications.Add(new DbClassification { TopicId = topic.TopicId, Name = "BFS" });
        await context.SaveChangesAsync();
        Classification classification = (await context.Classifications.ToListAsync())
            .Select(c => new Classification { ClassificationId = c.ClassificationId })
            .Single();

        await repository.AddClassificationsToProblemAsync(problem.ProblemId!.Value, new[] { classification });

        DbProblem reloaded = await context.Problems
            .Include(p => p.Classifications)
            .SingleAsync(p => p.ProblemId == problem.ProblemId);
        Assert.Equal(new[] { "BFS" }, reloaded.Classifications.Select(c => c.Name));
    }

    [Fact]
    public async Task RemoveAllClassificationsFromProblemAsync_ClearsClassifications()
    {
        using AppDbContext context = CreateContext();
        var repository = new TopicRepository(context, CreateMapper());
        DbProblem problem = await SeedProblemAsync(context);
        DbTopic topic = await SeedTopicAsync(context, "Graphs");
        context.Classifications.Add(new DbClassification { TopicId = topic.TopicId, Name = "BFS" });
        await context.SaveChangesAsync();
        DbClassification classification = await context.Classifications.SingleAsync();
        problem.Classifications = new List<DbClassification> { classification };
        await context.SaveChangesAsync();

        await repository.RemoveAllClassificationsFromProblemAsync(problem.ProblemId!.Value);

        DbProblem reloaded = await context.Problems
            .Include(p => p.Classifications)
            .SingleAsync(p => p.ProblemId == problem.ProblemId);
        Assert.Empty(reloaded.Classifications);
        // The classification itself must survive: this only detaches it
        // from the problem, it must not delete the shared row.
        Assert.Equal(1, await context.Classifications.CountAsync());
    }

    [Fact]
    public async Task RemoveAllClassificationsFromProblemAsync_ThrowsWhenProblemNotFound()
    {
        using AppDbContext context = CreateContext();
        var repository = new TopicRepository(context, CreateMapper());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.RemoveAllClassificationsFromProblemAsync(9999));
    }

    [Fact]
    public async Task UpdateClassification_UpdatesName()
    {
        using AppDbContext context = CreateContext();
        var repository = new TopicRepository(context, CreateMapper());
        DbTopic topic = await SeedTopicAsync(context, "Graphs");
        context.Classifications.Add(new DbClassification { TopicId = topic.TopicId, Name = "BFS" });
        await context.SaveChangesAsync();
        int classificationId = (await context.Classifications.SingleAsync()).ClassificationId;

        await repository.UpdateClassification(new Classification { Name = "BFS (renamed)" }, classificationId);

        Assert.Equal("BFS (renamed)", (await context.Classifications.SingleAsync()).Name);
    }

    [Fact]
    public async Task UpdateClassification_ThrowsWhenNotFound()
    {
        using AppDbContext context = CreateContext();
        var repository = new TopicRepository(context, CreateMapper());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.UpdateClassification(new Classification { Name = "x" }, 9999));
    }
}
