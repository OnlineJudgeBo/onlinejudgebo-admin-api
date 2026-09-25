using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
using static RepositoryTestSupport;

public class RoleRepositoryTests
{
    private static DbUser NewUser(string userId, int siteId) => new()
    {
        UserId = userId,
        Ip = "127.0.0.1",
        SiteId = siteId,
        IsActive = true,
        IsDeleted = false,
        UserProfile = new DbUserProfile { UserId = userId, SiteId = siteId, Nick = userId }
    };

    [Fact]
    public async Task AddRoleToUserAsync_PersistsUserRole()
    {
        using AppDbContext context = CreateContext();
        var repository = new RoleRepository(context, CreateMapper());
        context.Users.Add(NewUser("alice", siteId: 1));
        await context.SaveChangesAsync();

        await repository.AddRoleToUserAsync("alice", roleId: 2, siteId: 1);

        DbUserRole userRole = await context.UserRoles.SingleAsync();
        Assert.Equal("alice", userRole.UserId);
        Assert.Equal(2, userRole.RoleId);
        Assert.Equal(1, userRole.SiteId);
    }

    [Fact]
    public async Task AddRoleToUserAsync_ThrowsWhenSameUserRoleAddedTwice()
    {
        using AppDbContext context = CreateContext();
        var repository = new RoleRepository(context, CreateMapper());
        context.Users.Add(NewUser("alice", siteId: 1));
        await context.SaveChangesAsync();

        await repository.AddRoleToUserAsync("alice", roleId: 2, siteId: 1);

        // (UserId, RoleId) is the primary key: a second grant of the same
        // role must not silently double up, it must fail loudly.
        await Assert.ThrowsAnyAsync<Exception>(() => repository.AddRoleToUserAsync("alice", roleId: 2, siteId: 1));
    }

    [Fact]
    public async Task RemoveRoleFromUserAsync_RemovesMatchingRole()
    {
        using AppDbContext context = CreateContext();
        var repository = new RoleRepository(context, CreateMapper());
        context.Users.Add(NewUser("alice", siteId: 1));
        await context.SaveChangesAsync();
        await repository.AddRoleToUserAsync("alice", roleId: 2, siteId: 1);

        await repository.RemoveRoleFromUserAsync("alice", roleId: 2, siteId: 1);

        Assert.Empty(await context.UserRoles.ToListAsync());
    }

    [Fact]
    public async Task RemoveRoleFromUserAsync_IsNoOpWhenNotFound()
    {
        using AppDbContext context = CreateContext();
        var repository = new RoleRepository(context, CreateMapper());

        await repository.RemoveRoleFromUserAsync("nobody", roleId: 2, siteId: 1);

        Assert.Empty(await context.UserRoles.ToListAsync());
    }

    // Pins the query's actual (slightly surprising) behavior: the outer
    // filter only checks "has any user_roles row at all" — it does not
    // re-check RoleId != 1 the way the inner, per-user UserRoles projection
    // does. So a user whose only grant is the implicit RoleId 1 still shows
    // up in the result, just with an empty UserRoles list, rather than
    // being excluded outright. If that's not the intended behavior, the
    // outer .Where needs the same != 1 condition as the inner one.
    [Fact]
    public async Task GetUserRolesAsync_FiltersBySite_AndOnlyExcludesRoleIdOneFromEachUsersRoleList()
    {
        using AppDbContext context = CreateContext();
        var repository = new RoleRepository(context, CreateMapper());
        context.Roles.AddRange(
            new DbRole { RoleId = 1, RoleName = "Default" },
            new DbRole { RoleId = 2, RoleName = "Docente" });
        context.Users.Add(NewUser("alice", siteId: 1));
        context.Users.Add(NewUser("bob", siteId: 1));
        context.Users.Add(NewUser("carol", siteId: 2));
        await context.SaveChangesAsync();
        // alice: only the implicit RoleId 1.
        context.UserRoles.Add(new DbUserRole { UserId = "alice", RoleId = 1, SiteId = 1 });
        // bob: RoleId 2 -> must appear, with that role listed.
        context.UserRoles.Add(new DbUserRole { UserId = "bob", RoleId = 2, SiteId = 1 });
        // carol: RoleId 2 but a different site -> must not appear at all.
        context.UserRoles.Add(new DbUserRole { UserId = "carol", RoleId = 2, SiteId = 2 });
        await context.SaveChangesAsync();

        IEnumerable<User> result = await repository.GetUserRolesAsync(siteId: 1);

        Assert.Equal(new[] { "alice", "bob" }, result.Select(u => u.UserId));
        Assert.Empty(result.Single(u => u.UserId == "alice").UserRoles);
        Assert.Single(result.Single(u => u.UserId == "bob").UserRoles);
    }

    // GetUserRolesAsync's per-role projection must set RoleId on both the
    // flat DbUserRole scalar (what CreateMap<DbUserRole, UserRole>() maps
    // UserRole.RoleId from) and the nested Role navigation, since the two
    // are populated independently in the query.
    [Fact]
    public async Task GetUserRolesAsync_PopulatesRoleIdOnEachEntry()
    {
        using AppDbContext context = CreateContext();
        var repository = new RoleRepository(context, CreateMapper());
        context.Roles.Add(new DbRole { RoleId = 2, RoleName = "Docente" });
        context.Users.Add(NewUser("bob", siteId: 1));
        await context.SaveChangesAsync();
        context.UserRoles.Add(new DbUserRole { UserId = "bob", RoleId = 2, SiteId = 1 });
        await context.SaveChangesAsync();

        IEnumerable<User> result = await repository.GetUserRolesAsync(siteId: 1);

        UserRole role = result.Single().UserRoles.Single();
        Assert.Equal(2, role.RoleId);
        Assert.Equal("Docente", role.Role.RoleName);
    }

    [Fact]
    public async Task GetAllRolesAsync_ReturnsAllRoles()
    {
        using AppDbContext context = CreateContext();
        var repository = new RoleRepository(context, CreateMapper());
        context.Roles.AddRange(
            new DbRole { RoleId = 1, RoleName = "Default" },
            new DbRole { RoleId = 2, RoleName = "Docente" });
        await context.SaveChangesAsync();

        IEnumerable<Role> result = await repository.GetAllRolesAsync();

        Assert.Equal(new[] { "Default", "Docente" }, result.Select(r => r.RoleName));
    }

    [Fact]
    public async Task GetUserRoleAsync_ReturnsMatchingRoleForSite()
    {
        using AppDbContext context = CreateContext();
        var repository = new RoleRepository(context, CreateMapper());
        context.Users.Add(NewUser("alice", siteId: 1));
        await context.SaveChangesAsync();
        await repository.AddRoleToUserAsync("alice", roleId: 2, siteId: 1);

        UserRole result = await repository.GetUserRoleAsync("alice", siteId: 1);

        Assert.Equal(2, result.RoleId);
    }
}
