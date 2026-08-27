using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
using static RepositoryTestSupport;

// AcademicRepository straddles two DbContexts: AppDbContext (users,
// problems) and AcademicCatalogDbContext (courses, learning paths).
// DbCourse/DbCourseAssignment/DbCourseAssignmentProblem all use
// [DatabaseGenerated(Identity)] (the safe pattern, like DbSolution) and
// DbLearningPath/DbAcademicTopic/DbSubtopic use plain non-nullable `long`
// PKs (0 == CLR default, also safe) — so this file isn't chasing the
// ProblemRepository FK bug specifically, it's pinning down the real
// mutation logic (membership rules, duplicate-key guards, problem-set
// replacement, progress-key migration) the same way the other repository
// test files do for their own repositories.
public class AcademicRepositoryTests
{
    private static async Task<int> SeedProblemAsync(AppDbContext context, int siteId, string title = "Problem A")
    {
        if (!await context.Sites.AnyAsync(site => site.SiteId == siteId))
        {
            context.Sites.Add(new DbSite { SiteId = siteId, Name = "Site " + siteId });
        }
        var problem = new DbProblem { Title = title, Spj = "N", Defunct = "N", TimeLimit = 1000, MemoryLimit = 128 };
        context.Problems.Add(problem);
        await context.SaveChangesAsync();
        context.ProblemSites.Add(new DbProblemSite { problemId = problem.ProblemId!.Value, SiteId = siteId, IsActive = true });
        await context.SaveChangesAsync();
        return problem.ProblemId!.Value;
    }

    private static async Task SeedUserAsync(AppDbContext context, string userId, int siteId, bool active = true)
    {
        context.Users.Add(new DbUser { UserId = userId, Ip = "127.0.0.1", SiteId = siteId, IsActive = active, IsDeleted = false });
        await context.SaveChangesAsync();
    }

    // ---- Course membership --------------------------------------------

    [Fact]
    public async Task CreateCourseAsync_AssignsGeneratedId_AndMakesCreatorTheTeacher()
    {
        using AppDbContext context = CreateContext();
        using AcademicCatalogDbContext academicContext = CreateAcademicContext();
        var repository = new AcademicRepository(context, academicContext);

        AcademicCourseDetail created = await repository.CreateCourseAsync(1, "teacher1",
            new AcademicCourseCreationRequest { Name = "Algorithms 101" });

        Assert.NotEqual(0, created.CourseId);
        Assert.Equal("teacher1", created.OwnerUserId);
        Assert.Equal(CourseRoleNames.Teacher, created.MemberRole);
        DbCourseUser membership = await academicContext.CourseUsers.SingleAsync();
        Assert.Equal(created.CourseId, membership.CourseId);
        Assert.Equal(CourseRoleNames.Teacher, membership.Role);
    }

    [Fact]
    public async Task JoinCourseAsync_AddsStudentMembership_ForValidInviteCode()
    {
        using AppDbContext context = CreateContext();
        using AcademicCatalogDbContext academicContext = CreateAcademicContext();
        var repository = new AcademicRepository(context, academicContext);
        AcademicCourseDetail course = await repository.CreateCourseAsync(1, "teacher1", new AcademicCourseCreationRequest { Name = "Algorithms 101" });
        string inviteCode = (await academicContext.Courses.SingleAsync()).InviteCode;

        AcademicCourseDetail joined = await repository.JoinCourseAsync(1, "student1", new AcademicJoinCourseRequest { InviteCode = inviteCode });

        Assert.Equal(CourseRoleNames.Student, joined.MemberRole);
        Assert.Equal(2, await academicContext.CourseUsers.CountAsync());
    }

    [Fact]
    public async Task JoinCourseAsync_ThrowsForInvalidInviteCode()
    {
        using AppDbContext context = CreateContext();
        using AcademicCatalogDbContext academicContext = CreateAcademicContext();
        var repository = new AcademicRepository(context, academicContext);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.JoinCourseAsync(1, "student1", new AcademicJoinCourseRequest { InviteCode = "NOPE" }));
    }

    [Fact]
    public async Task AddCourseMemberAsync_AddsNewMemberWithGivenRole()
    {
        using AppDbContext context = CreateContext();
        using AcademicCatalogDbContext academicContext = CreateAcademicContext();
        var repository = new AcademicRepository(context, academicContext);
        AcademicCourseDetail course = await repository.CreateCourseAsync(1, "teacher1", new AcademicCourseCreationRequest { Name = "Algorithms 101" });
        await SeedUserAsync(context, "assistant1", siteId: 1);

        AcademicCourseMember member = await repository.AddCourseMemberAsync(1, course.CourseId,
            new AcademicCourseMemberCreationRequest { UserId = "assistant1", Role = CourseRoleNames.Assistant });

        Assert.Equal(CourseRoleNames.Assistant, member.Role);
    }

    [Fact]
    public async Task AddCourseMemberAsync_ThrowsWhenUserNotActiveInSite()
    {
        using AppDbContext context = CreateContext();
        using AcademicCatalogDbContext academicContext = CreateAcademicContext();
        var repository = new AcademicRepository(context, academicContext);
        AcademicCourseDetail course = await repository.CreateCourseAsync(1, "teacher1", new AcademicCourseCreationRequest { Name = "Algorithms 101" });
        await SeedUserAsync(context, "inactive1", siteId: 1, active: false);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.AddCourseMemberAsync(1, course.CourseId,
                new AcademicCourseMemberCreationRequest { UserId = "inactive1", Role = CourseRoleNames.Student }));
    }

    [Fact]
    public async Task AddCourseMemberAsync_ThrowsWhenEditingTeacherMembership()
    {
        using AppDbContext context = CreateContext();
        using AcademicCatalogDbContext academicContext = CreateAcademicContext();
        var repository = new AcademicRepository(context, academicContext);
        AcademicCourseDetail course = await repository.CreateCourseAsync(1, "teacher1", new AcademicCourseCreationRequest { Name = "Algorithms 101" });
        await SeedUserAsync(context, "teacher1", siteId: 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.AddCourseMemberAsync(1, course.CourseId,
                new AcademicCourseMemberCreationRequest { UserId = "teacher1", Role = CourseRoleNames.Student }));
    }

    [Fact]
    public async Task RemoveCourseMemberAsync_RemovesNonTeacherMember()
    {
        using AppDbContext context = CreateContext();
        using AcademicCatalogDbContext academicContext = CreateAcademicContext();
        var repository = new AcademicRepository(context, academicContext);
        AcademicCourseDetail course = await repository.CreateCourseAsync(1, "teacher1", new AcademicCourseCreationRequest { Name = "Algorithms 101" });
        await SeedUserAsync(context, "student1", siteId: 1);
        await repository.AddCourseMemberAsync(1, course.CourseId, new AcademicCourseMemberCreationRequest { UserId = "student1", Role = CourseRoleNames.Student });

        await repository.RemoveCourseMemberAsync(1, course.CourseId, "student1");

        Assert.Single(await academicContext.CourseUsers.ToListAsync());
    }

    [Fact]
    public async Task RemoveCourseMemberAsync_ThrowsWhenRemovingTeacher()
    {
        using AppDbContext context = CreateContext();
        using AcademicCatalogDbContext academicContext = CreateAcademicContext();
        var repository = new AcademicRepository(context, academicContext);
        AcademicCourseDetail course = await repository.CreateCourseAsync(1, "teacher1", new AcademicCourseCreationRequest { Name = "Algorithms 101" });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.RemoveCourseMemberAsync(1, course.CourseId, "teacher1"));
    }

    // ---- Course assignments ---------------------------------------------

    [Fact]
    public async Task CreateCourseAssignmentAsync_PersistsAssignmentProblemsAndContentItem()
    {
        using AppDbContext context = CreateContext();
        using AcademicCatalogDbContext academicContext = CreateAcademicContext();
        var repository = new AcademicRepository(context, academicContext);
        AcademicCourseDetail course = await repository.CreateCourseAsync(1, "teacher1", new AcademicCourseCreationRequest { Name = "Algorithms 101" });
        int problemId = await SeedProblemAsync(context, siteId: 1);

        AcademicCourseAssignment assignment = await repository.CreateCourseAssignmentAsync(1, course.CourseId, "teacher1",
            new AcademicCourseAssignmentCreationRequest { Title = "Week 1", ProblemIds = new List<int> { problemId } });

        Assert.NotEqual(0, assignment.AssignmentId);
        DbCourseAssignmentProblem link = await academicContext.CourseAssignmentProblems.SingleAsync();
        Assert.Equal(assignment.AssignmentId, link.AssignmentId);
        Assert.Equal(problemId, link.ProblemId);
        DbCourseContentItem contentItem = await academicContext.CourseContentItems.SingleAsync();
        Assert.Equal(assignment.AssignmentId, contentItem.AssignmentId);
        Assert.Equal("contest", contentItem.ItemType);
    }

    [Fact]
    public async Task CreateCourseAssignmentAsync_ThrowsWhenProblemNotAvailableInSite()
    {
        using AppDbContext context = CreateContext();
        using AcademicCatalogDbContext academicContext = CreateAcademicContext();
        var repository = new AcademicRepository(context, academicContext);
        AcademicCourseDetail course = await repository.CreateCourseAsync(1, "teacher1", new AcademicCourseCreationRequest { Name = "Algorithms 101" });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.CreateCourseAssignmentAsync(1, course.CourseId, "teacher1",
                new AcademicCourseAssignmentCreationRequest { Title = "Week 1", ProblemIds = new List<int> { 9999 } }));
    }

    [Fact]
    public async Task UpdateCourseAssignmentAsync_ReplacesProblemSet()
    {
        using AppDbContext context = CreateContext();
        using AcademicCatalogDbContext academicContext = CreateAcademicContext();
        var repository = new AcademicRepository(context, academicContext);
        AcademicCourseDetail course = await repository.CreateCourseAsync(1, "teacher1", new AcademicCourseCreationRequest { Name = "Algorithms 101" });
        int problem1 = await SeedProblemAsync(context, siteId: 1, title: "Problem A");
        int problem2 = await SeedProblemAsync(context, siteId: 1, title: "Problem B");
        AcademicCourseAssignment assignment = await repository.CreateCourseAssignmentAsync(1, course.CourseId, "teacher1",
            new AcademicCourseAssignmentCreationRequest { Title = "Week 1", ProblemIds = new List<int> { problem1 } });

        await repository.UpdateCourseAssignmentAsync(1, course.CourseId, assignment.AssignmentId, "teacher1",
            new AcademicCourseAssignmentCreationRequest { Title = "Week 1 updated", ProblemIds = new List<int> { problem2 } });

        DbCourseAssignmentProblem link = await academicContext.CourseAssignmentProblems.SingleAsync();
        Assert.Equal(problem2, link.ProblemId);
        Assert.Equal("Week 1 updated", (await academicContext.CourseAssignments.SingleAsync()).Title);
    }

    // ---- Course materials -----------------------------------------------

    [Fact]
    public async Task CreateCourseMaterialAsync_AssignsIncrementingPosition()
    {
        using AppDbContext context = CreateContext();
        using AcademicCatalogDbContext academicContext = CreateAcademicContext();
        var repository = new AcademicRepository(context, academicContext);
        AcademicCourseDetail course = await repository.CreateCourseAsync(1, "teacher1", new AcademicCourseCreationRequest { Name = "Algorithms 101" });

        AcademicCourseContentItem first = await repository.CreateCourseMaterialAsync(course.CourseId, "teacher1",
            new AcademicCourseMaterialCreationRequest { Title = "Slides 1" });
        AcademicCourseContentItem second = await repository.CreateCourseMaterialAsync(course.CourseId, "teacher1",
            new AcademicCourseMaterialCreationRequest { Title = "Slides 2" });

        Assert.True(second.Position > first.Position);
    }

    [Fact]
    public async Task UpdateCourseMaterialAsync_UpdatesFields_AndThrowsWhenNotFound()
    {
        using AppDbContext context = CreateContext();
        using AcademicCatalogDbContext academicContext = CreateAcademicContext();
        var repository = new AcademicRepository(context, academicContext);
        AcademicCourseDetail course = await repository.CreateCourseAsync(1, "teacher1", new AcademicCourseCreationRequest { Name = "Algorithms 101" });
        AcademicCourseContentItem material = await repository.CreateCourseMaterialAsync(course.CourseId, "teacher1",
            new AcademicCourseMaterialCreationRequest { Title = "Slides 1" });

        AcademicCourseContentItem updated = await repository.UpdateCourseMaterialAsync(course.CourseId, material.ItemId,
            new AcademicCourseMaterialCreationRequest { Title = "Slides 1 (updated)", IsPublished = false });

        Assert.Equal("Slides 1 (updated)", updated.Title);
        Assert.False(updated.IsPublished);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            repository.UpdateCourseMaterialAsync(course.CourseId, materialId: 9999,
                new AcademicCourseMaterialCreationRequest { Title = "x" }));
    }

    [Fact]
    public async Task DeleteCourseMaterialAsync_RemovesMaterial()
    {
        using AppDbContext context = CreateContext();
        using AcademicCatalogDbContext academicContext = CreateAcademicContext();
        var repository = new AcademicRepository(context, academicContext);
        AcademicCourseDetail course = await repository.CreateCourseAsync(1, "teacher1", new AcademicCourseCreationRequest { Name = "Algorithms 101" });
        AcademicCourseContentItem material = await repository.CreateCourseMaterialAsync(course.CourseId, "teacher1",
            new AcademicCourseMaterialCreationRequest { Title = "Slides 1" });

        await repository.DeleteCourseMaterialAsync(course.CourseId, material.ItemId);

        Assert.Empty(await academicContext.CourseContentItems.ToListAsync());
    }

    // ---- Learning paths --------------------------------------------------

    [Fact]
    public async Task CreateLearningPathAsync_AssignsGeneratedId_AndThrowsOnDuplicateKey()
    {
        using AppDbContext context = CreateContext();
        using AcademicCatalogDbContext academicContext = CreateAcademicContext();
        var repository = new AcademicRepository(context, academicContext);
        var request = new LearningPathAdminUpsertRequest { Key = "algo101", Title = "Algorithms", LanguagePrimary = "cpp", Category = "core" };

        LearningPathResponse created = await repository.CreateLearningPathAsync(1, request);

        Assert.Equal("algo101", created.Track.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateLearningPathAsync(1, request));
    }

    [Fact]
    public async Task UpdateLearningPathAsync_UpdatesFields_AndThrowsIfKeyChanges()
    {
        using AppDbContext context = CreateContext();
        using AcademicCatalogDbContext academicContext = CreateAcademicContext();
        var repository = new AcademicRepository(context, academicContext);
        await repository.CreateLearningPathAsync(1, new LearningPathAdminUpsertRequest { Key = "algo101", Title = "Algorithms", LanguagePrimary = "cpp", Category = "core" });

        LearningPathResponse updated = await repository.UpdateLearningPathAsync(1, "algo101",
            new LearningPathAdminUpsertRequest { Key = "algo101", Title = "Algorithms Updated", LanguagePrimary = "cpp", Category = "core" });

        Assert.Equal("Algorithms Updated", updated.Track.Title);

        await Assert.ThrowsAsync<ArgumentException>(() => repository.UpdateLearningPathAsync(1, "algo101",
            new LearningPathAdminUpsertRequest { Key = "algo102", Title = "x", LanguagePrimary = "cpp", Category = "core" }));
    }

    [Fact]
    public async Task DeleteLearningPathAsync_RemovesPath()
    {
        using AppDbContext context = CreateContext();
        using AcademicCatalogDbContext academicContext = CreateAcademicContext();
        var repository = new AcademicRepository(context, academicContext);
        await repository.CreateLearningPathAsync(1, new LearningPathAdminUpsertRequest { Key = "algo101", Title = "Algorithms", LanguagePrimary = "cpp", Category = "core" });

        await repository.DeleteLearningPathAsync(1, "algo101");

        Assert.Empty(await academicContext.LearningPaths.ToListAsync());
    }

    [Fact]
    public async Task CreateLearningPathStageAsync_AddsStage_AndThrowsOnDuplicateOrder()
    {
        // Sqlite: CreateLearningPathStageAsync wraps its inserts in a real
        // transaction (BeginTransactionAsync), which the InMemory provider
        // does not support.
        using SqliteContext<AcademicCatalogDbContext> scope = CreateSqliteContext<AcademicCatalogDbContext>(o => new SqliteAcademicCatalogDbContext(o));
        AcademicCatalogDbContext academicContext = scope.Context;
        using AppDbContext context = CreateContext();
        var repository = new AcademicRepository(context, academicContext);
        await repository.CreateLearningPathAsync(1, new LearningPathAdminUpsertRequest { Key = "algo101", Title = "Algorithms", LanguagePrimary = "cpp", Category = "core" });

        LearningPathResponse withStage = await repository.CreateLearningPathStageAsync(1, "algo101",
            new LearningPathStageAdminRequest { Key = "arrays", Name = "Arrays", Order = 1 });

        Assert.Single(withStage.Stages);

        await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateLearningPathStageAsync(1, "algo101",
            new LearningPathStageAdminRequest { Key = "arrays-2", Name = "Arrays 2", Order = 1 }));
    }

    [Fact]
    public async Task UpdateLearningPathStageAsync_UpdatesFields()
    {
        using SqliteContext<AcademicCatalogDbContext> scope = CreateSqliteContext<AcademicCatalogDbContext>(o => new SqliteAcademicCatalogDbContext(o));
        AcademicCatalogDbContext academicContext = scope.Context;
        using AppDbContext context = CreateContext();
        var repository = new AcademicRepository(context, academicContext);
        await repository.CreateLearningPathAsync(1, new LearningPathAdminUpsertRequest { Key = "algo101", Title = "Algorithms", LanguagePrimary = "cpp", Category = "core" });
        await repository.CreateLearningPathStageAsync(1, "algo101", new LearningPathStageAdminRequest { Key = "arrays", Name = "Arrays", Order = 1 });
        long stageId = (await academicContext.Topics.SingleAsync()).TopicId;

        LearningPathResponse updated = await repository.UpdateLearningPathStageAsync(1, "algo101", stageId,
            new LearningPathStageAdminRequest { Key = "arrays", Name = "Arrays (renamed)", Order = 1 });

        Assert.Equal("Arrays (renamed)", updated.Stages.Single().Name);
    }

    [Fact]
    public async Task DeleteLearningPathStageAsync_RemovesStage_WhenNotShared()
    {
        using SqliteContext<AcademicCatalogDbContext> scope = CreateSqliteContext<AcademicCatalogDbContext>(o => new SqliteAcademicCatalogDbContext(o));
        AcademicCatalogDbContext academicContext = scope.Context;
        using AppDbContext context = CreateContext();
        var repository = new AcademicRepository(context, academicContext);
        await repository.CreateLearningPathAsync(1, new LearningPathAdminUpsertRequest { Key = "algo101", Title = "Algorithms", LanguagePrimary = "cpp", Category = "core" });
        await repository.CreateLearningPathStageAsync(1, "algo101", new LearningPathStageAdminRequest { Key = "arrays", Name = "Arrays", Order = 1 });
        long stageId = (await academicContext.Topics.SingleAsync()).TopicId;

        await repository.DeleteLearningPathStageAsync(1, "algo101", stageId);

        Assert.Empty(await academicContext.Topics.ToListAsync());
        Assert.Empty(await academicContext.LearningPathTopics.ToListAsync());
    }

    [Fact]
    public async Task CreateLearningPathTopicAsync_AddsTopicWithValidatedProblems()
    {
        using SqliteContext<AcademicCatalogDbContext> scope = CreateSqliteContext<AcademicCatalogDbContext>(o => new SqliteAcademicCatalogDbContext(o));
        AcademicCatalogDbContext academicContext = scope.Context;
        using AppDbContext context = CreateContext();
        var repository = new AcademicRepository(context, academicContext);
        int problemId = await SeedProblemAsync(context, siteId: 1);
        await repository.CreateLearningPathAsync(1, new LearningPathAdminUpsertRequest { Key = "algo101", Title = "Algorithms", LanguagePrimary = "cpp", Category = "core" });
        await repository.CreateLearningPathStageAsync(1, "algo101", new LearningPathStageAdminRequest { Key = "arrays", Name = "Arrays", Order = 1 });
        long stageId = (await academicContext.Topics.SingleAsync()).TopicId;

        await repository.CreateLearningPathTopicAsync(1, "algo101", stageId,
            new LearningPathTopicAdminRequest { Key = "two_pointers", Title = "Two Pointers", Order = 1, ProblemIds = new List<int> { problemId } });

        DbSubtopicProblem link = await academicContext.SubtopicProblems.SingleAsync();
        Assert.Equal(problemId, link.ProblemId);
    }

    [Fact]
    public async Task UpdateLearningPathTopicAsync_ReplacesProblems_AndMigratesLastTopicIdOnRename()
    {
        // Sqlite: setup goes through CreateLearningPathTopicAsync, which
        // wraps its inserts in a real transaction.
        using SqliteContext<AcademicCatalogDbContext> scope = CreateSqliteContext<AcademicCatalogDbContext>(o => new SqliteAcademicCatalogDbContext(o));
        AcademicCatalogDbContext academicContext = scope.Context;
        using AppDbContext context = CreateContext();
        var repository = new AcademicRepository(context, academicContext);
        int problem1 = await SeedProblemAsync(context, siteId: 1, title: "Problem A");
        int problem2 = await SeedProblemAsync(context, siteId: 1, title: "Problem B");
        await repository.CreateLearningPathAsync(1, new LearningPathAdminUpsertRequest { Key = "algo101", Title = "Algorithms", LanguagePrimary = "cpp", Category = "core" });
        var path = await academicContext.LearningPaths.SingleAsync();
        var stage = new DbAcademicTopic { TopicKey = "arrays", Name = "Arrays", SortOrder = 1 };
        academicContext.Topics.Add(stage);
        await academicContext.SaveChangesAsync();
        academicContext.LearningPathTopics.Add(new DbLearningPathTopic { LearningPathId = path.LearningPathId, TopicId = stage.TopicId });
        await academicContext.SaveChangesAsync();
        await repository.CreateLearningPathTopicAsync(1, "algo101", stage.TopicId,
            new LearningPathTopicAdminRequest { Key = "two_pointers", Title = "Two Pointers", Order = 1, ProblemIds = new List<int> { problem1 } });
        long topicId = (await academicContext.Subtopics.SingleAsync()).SubtopicId;
        // LastTopicId is a plain column (not a key component), so renaming
        // the topic key must migrate it for anyone currently "on" this topic.
        academicContext.LearningPathProgresses.Add(new DbLearningPathProgress { LearningPathId = path.LearningPathId, UserId = "student1", LastTopicId = "two_pointers" });
        await academicContext.SaveChangesAsync();

        await repository.UpdateLearningPathTopicAsync(1, "algo101", stage.TopicId, topicId,
            new LearningPathTopicAdminRequest { Key = "two_pointers_renamed", Title = "Two Pointers", Order = 1, ProblemIds = new List<int> { problem2 } });

        DbSubtopicProblem link = await academicContext.SubtopicProblems.SingleAsync();
        Assert.Equal(problem2, link.ProblemId);
        Assert.Equal("two_pointers_renamed", (await academicContext.LearningPathProgresses.SingleAsync()).LastTopicId);
    }

    // DbLearningPathTopicProgress's primary key is (LearningPathId, UserId,
    // TopicId) — TopicId is a key component, not a plain column, so it
    // can't be updated in place; UpdateLearningPathTopicAsync must delete
    // the old-keyed rows and re-insert them under the new key instead.
    [Fact]
    public async Task UpdateLearningPathTopicAsync_MigratesTopicProgress_WhenRenamingKey()
    {
        using SqliteContext<AcademicCatalogDbContext> scope = CreateSqliteContext<AcademicCatalogDbContext>(o => new SqliteAcademicCatalogDbContext(o));
        AcademicCatalogDbContext academicContext = scope.Context;
        using AppDbContext context = CreateContext();
        var repository = new AcademicRepository(context, academicContext);
        int problemId = await SeedProblemAsync(context, siteId: 1);
        await repository.CreateLearningPathAsync(1, new LearningPathAdminUpsertRequest { Key = "algo101", Title = "Algorithms", LanguagePrimary = "cpp", Category = "core" });
        var path = await academicContext.LearningPaths.SingleAsync();
        var stage = new DbAcademicTopic { TopicKey = "arrays", Name = "Arrays", SortOrder = 1 };
        academicContext.Topics.Add(stage);
        await academicContext.SaveChangesAsync();
        academicContext.LearningPathTopics.Add(new DbLearningPathTopic { LearningPathId = path.LearningPathId, TopicId = stage.TopicId });
        await academicContext.SaveChangesAsync();
        await repository.CreateLearningPathTopicAsync(1, "algo101", stage.TopicId,
            new LearningPathTopicAdminRequest { Key = "two_pointers", Title = "Two Pointers", Order = 1, ProblemIds = new List<int> { problemId } });
        long topicId = (await academicContext.Subtopics.SingleAsync()).SubtopicId;
        var completedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        academicContext.LearningPathTopicProgresses.Add(new DbLearningPathTopicProgress { LearningPathId = path.LearningPathId, UserId = "student1", TopicId = "two_pointers", CompletedAt = completedAt });
        await academicContext.SaveChangesAsync();

        await repository.UpdateLearningPathTopicAsync(1, "algo101", stage.TopicId, topicId,
            new LearningPathTopicAdminRequest { Key = "two_pointers_renamed", Title = "Two Pointers", Order = 1, ProblemIds = new List<int> { problemId } });

        DbLearningPathTopicProgress migrated = await academicContext.LearningPathTopicProgresses.SingleAsync();
        Assert.Equal("two_pointers_renamed", migrated.TopicId);
        Assert.Equal("student1", migrated.UserId);
        Assert.Equal(completedAt, migrated.CompletedAt);
    }

    [Fact]
    public async Task DeleteLearningPathTopicAsync_RemovesTopicAndClearsProgress()
    {
        // Sqlite: setup goes through CreateLearningPathTopicAsync, which
        // wraps its inserts in a real transaction.
        using SqliteContext<AcademicCatalogDbContext> scope = CreateSqliteContext<AcademicCatalogDbContext>(o => new SqliteAcademicCatalogDbContext(o));
        AcademicCatalogDbContext academicContext = scope.Context;
        using AppDbContext context = CreateContext();
        var repository = new AcademicRepository(context, academicContext);
        await repository.CreateLearningPathAsync(1, new LearningPathAdminUpsertRequest { Key = "algo101", Title = "Algorithms", LanguagePrimary = "cpp", Category = "core" });
        var path = await academicContext.LearningPaths.SingleAsync();
        var stage = new DbAcademicTopic { TopicKey = "arrays", Name = "Arrays", SortOrder = 1 };
        academicContext.Topics.Add(stage);
        await academicContext.SaveChangesAsync();
        academicContext.LearningPathTopics.Add(new DbLearningPathTopic { LearningPathId = path.LearningPathId, TopicId = stage.TopicId });
        await academicContext.SaveChangesAsync();
        await repository.CreateLearningPathTopicAsync(1, "algo101", stage.TopicId,
            new LearningPathTopicAdminRequest { Key = "two_pointers", Title = "Two Pointers", Order = 1 });
        long topicId = (await academicContext.Subtopics.SingleAsync()).SubtopicId;
        academicContext.LearningPathProgresses.Add(new DbLearningPathProgress { LearningPathId = path.LearningPathId, UserId = "student1", LastTopicId = "two_pointers" });
        await academicContext.SaveChangesAsync();

        await repository.DeleteLearningPathTopicAsync(1, "algo101", stage.TopicId, topicId);

        Assert.Empty(await academicContext.Subtopics.ToListAsync());
        Assert.Null((await academicContext.LearningPathProgresses.SingleAsync()).LastTopicId);
    }
}
