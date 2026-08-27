using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
using static RepositoryTestSupport;

// UpdateUser renames user_id (the PK of `users`) via raw SQL, and that
// value is duplicated - with no real cascading FK in production, per the
// original bug report - across ~14 other tables split across two
// DbContexts. This file pins that the rename now fans out to all of them
// instead of leaving the other tables silently pointing at a user_id that
// no longer exists. Runs on Sqlite (needs BeginTransactionAsync, which
// InMemory doesn't support) with FK enforcement turned off on both
// connections: EF's C# model declares real FK constraints for some of
// these tables (user_profiles, user_activity, user_settings, news,
// user_roles, contest_user) that the live schema evidently doesn't
// actually enforce - that's the only way the original bug (a rename that
// "succeeds" while leaving orphans) is possible in the first place.
public class UserRepositoryTests
{
    [Fact]
    public async Task UpdateUser_CascadesRename_AcrossEveryDependentTable()
    {
        using SqliteContext<AppDbContext> appScope = CreateSqliteContext<AppDbContext>(o => new SqliteAppDbContext(o));
        using SqliteContext<AcademicCatalogDbContext> academicScope = CreateSqliteContext<AcademicCatalogDbContext>(o => new SqliteAcademicCatalogDbContext(o));
        AppDbContext context = appScope.Context;
        AcademicCatalogDbContext academicContext = academicScope.Context;
        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        await academicContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");

        const string oldUserId = "alice";
        const string newUserId = "alice2";
        context.Users.Add(new DbUser { UserId = oldUserId, Ip = "127.0.0.1", SiteId = 1, IsActive = true, IsDeleted = false });
        context.Privilege.Add(new DbPrivilege { UserId = oldUserId, Rightstr = "p1", Defunct = "N" });
        context.UserProfiles.Add(new DbUserProfile { UserId = oldUserId, SiteId = 1, Nick = oldUserId });
        context.UserSettings.Add(new DbUserSetting { UserId = oldUserId, SiteId = 1 });
        context.UserActivities.Add(new DbUserActivity { UserId = oldUserId, SiteId = 1 });
        context.News.Add(new DbNews { UserId = oldUserId, Title = "t", Content = "c", Defunct = "N" });
        context.Loginlogs.Add(new DbLoginlog { UserId = oldUserId });
        context.UserRoles.Add(new DbUserRole { UserId = oldUserId, RoleId = 2, SiteId = 1 });
        context.Sites.Add(new DbSite { SiteId = 1, Name = "Site 1" });
        context.Contests.Add(new DbContest { ContestId = 1, Title = "Contest A", StartTime = DateTime.Now, EndTime = DateTime.Now.AddDays(1), Defunct = "N" });
        await context.SaveChangesAsync();
        context.ContestUsers.Add(new DbContestUser { ContestId = 1, UserId = oldUserId, SiteId = 1, IsOwner = false });
        context.Solutions.Add(new DbSolution { ProblemId = 1, UserId = oldUserId, Ip = "127.0.0.1", InDate = DateTime.Now, Result = 0, Language = 1, SiteId = 1 });
        await context.SaveChangesAsync();
        context.CustomInputs.Add(new DbCustomInput { SolutionId = (await context.Solutions.SingleAsync()).SolutionId, ProblemId = 1, UserId = oldUserId, SiteId = 1, CreatedAt = DateTime.Now });
        await context.SaveChangesAsync();

        academicContext.CourseUsers.Add(new DbCourseUser { CourseId = 1, UserId = oldUserId, Role = "estudiante" });
        academicContext.LearningPathProgresses.Add(new DbLearningPathProgress { LearningPathId = 1, UserId = oldUserId });
        academicContext.LearningPathTopicProgresses.Add(new DbLearningPathTopicProgress { LearningPathId = 1, UserId = oldUserId, TopicId = "arrays" });
        academicContext.CourseSubmissionContexts.Add(new DbCourseSubmissionContext { SolutionId = 1, CourseId = 1, AssignmentId = 1, UserId = oldUserId });
        await academicContext.SaveChangesAsync();

        var repository = new UserRepository(context, academicContext, CreateMapper());

        await repository.UpdateUser(new User { UserId = newUserId }, oldUserId, siteId: 1);

        Assert.Equal(newUserId, (await context.Users.AsNoTracking().SingleAsync()).UserId);
        Assert.Equal(newUserId, (await context.Privilege.AsNoTracking().SingleAsync()).UserId);
        Assert.Equal(newUserId, (await context.UserProfiles.AsNoTracking().SingleAsync()).UserId);
        Assert.Equal(newUserId, (await context.UserSettings.AsNoTracking().SingleAsync()).UserId);
        Assert.Equal(newUserId, (await context.UserActivities.AsNoTracking().SingleAsync()).UserId);
        Assert.Equal(newUserId, (await context.News.AsNoTracking().SingleAsync()).UserId);
        Assert.Equal(newUserId, (await context.Loginlogs.AsNoTracking().SingleAsync()).UserId);
        Assert.Equal(newUserId, (await context.UserRoles.AsNoTracking().SingleAsync()).UserId);
        Assert.Equal(newUserId, (await context.ContestUsers.AsNoTracking().SingleAsync()).UserId);
        Assert.Equal(newUserId, (await context.Solutions.AsNoTracking().SingleAsync()).UserId);
        Assert.Equal(newUserId, (await context.CustomInputs.AsNoTracking().SingleAsync()).UserId);
        Assert.Equal(newUserId, (await academicContext.CourseUsers.AsNoTracking().SingleAsync()).UserId);
        Assert.Equal(newUserId, (await academicContext.LearningPathProgresses.AsNoTracking().SingleAsync()).UserId);
        Assert.Equal(newUserId, (await academicContext.LearningPathTopicProgresses.AsNoTracking().SingleAsync()).UserId);
        Assert.Equal(newUserId, (await academicContext.CourseSubmissionContexts.AsNoTracking().SingleAsync()).UserId);

        // Nothing must be left behind under the old id.
        Assert.False(await context.Users.AsNoTracking().AnyAsync(u => u.UserId == oldUserId));
        Assert.False(await context.UserRoles.AsNoTracking().AnyAsync(u => u.UserId == oldUserId));
        Assert.False(await academicContext.CourseUsers.AsNoTracking().AnyAsync(u => u.UserId == oldUserId));
    }

    [Fact]
    public async Task GetUserById_ReturnsNull_WhenUserNotFound()
    {
        using AppDbContext context = CreateContext();
        var repository = new UserRepository(context, CreateAcademicContext(), CreateMapper());

        User? result = await repository.GetUserById("nobody", siteId: 1);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserById_ReturnsUser_WhenFound()
    {
        using AppDbContext context = CreateContext();
        context.Users.Add(new DbUser { UserId = "alice", Ip = "127.0.0.1", SiteId = 1, IsActive = true, IsDeleted = false });
        await context.SaveChangesAsync();
        var repository = new UserRepository(context, CreateAcademicContext(), CreateMapper());

        User? result = await repository.GetUserById("alice", siteId: 1);

        Assert.NotNull(result);
        Assert.Equal("alice", result!.UserId);
    }
}
