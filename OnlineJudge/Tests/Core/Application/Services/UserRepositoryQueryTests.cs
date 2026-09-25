using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
using static RepositoryTestSupport;

public class UserRepositoryQueryTests
{
    private static UserRepository Repository(JudgeSeed seed) => new(seed.Db, seed.Academic, CreateMapper());

    private static JudgeSeed Users()
    {
        var seed = new JudgeSeed();
        seed.User("ana", nick: "Ana").User("bob", nick: "Roberto").User("off", active: false).User("site2", siteId: 2);
        seed.Db.UserProfiles.Single(profile => profile.UserId == "bob").Lastname = "Quispe";
        seed.Save();
        return seed;
    }

    [Fact]
    public async Task GetAllUsersProfiles_OnlyActiveUsersOfSiteOrderedById()
    {
        using var seed = Users();

        var users = (await Repository(seed).GetAllUsersProfilesAsync(1)).ToList();

        Assert.Equal(new[] { "ana", "bob" }, users.Select(user => user.UserId));
        Assert.Equal("Roberto", users[1].UserProfile.Nick);
    }

    [Theory]
    [InlineData("Rob", "bob")]
    [InlineData("Quispe", "bob")]
    [InlineData("ana@mail", "ana")]
    [InlineData("an", "ana")]
    public async Task SearchUserProfiles_MatchesNickLastnameEmailOrId(string term, string expected)
    {
        using var seed = Users();

        var users = await Repository(seed).SearchUserProfilesAsync(term, 1);

        Assert.Equal(expected, Assert.Single(users).UserId);
    }

    [Fact]
    public async Task SearchUserProfiles_BlankTermListsAllAndSkipsOtherSites()
    {
        using var seed = Users();

        Assert.Equal(2, (await Repository(seed).SearchUserProfilesAsync(" ", 1)).Count());
        Assert.Empty(await Repository(seed).SearchUserProfilesAsync("site2", 1));
    }

    [Fact]
    public async Task Availability_UserIdIsGlobalEmailIsPerSite()
    {
        using var seed = Users();
        var repository = Repository(seed);

        Assert.False(await repository.CheckUsernameAvailable(new UserProfile { UserId = "site2" }, 1));
        Assert.True(await repository.CheckUsernameAvailable(new UserProfile { UserId = "free" }, 1));
        Assert.False(await repository.CheckUserEmailAvailable(new UserProfile { Email = "ana@mail.bo" }, 1));
        Assert.True(await repository.CheckUserEmailAvailable(new UserProfile { Email = "ana@mail.bo" }, 2));
        Assert.True(await repository.UserIdExists("bob", "ana"));
        Assert.False(await repository.UserIdExists("ana", "ana"));
    }

    [Fact]
    public async Task GetUserById_ScopedToSite()
    {
        using var seed = Users();

        Assert.Equal("ana", (await Repository(seed).GetUserById("ana", 1)).UserId);
        Assert.Null(await Repository(seed).GetUserById("ana", 2));
    }

    [Fact]
    public async Task UpdateUserProfile_UpdatesExistingAndReturnsNullWhenMissing()
    {
        using var seed = Users();
        var repository = Repository(seed);

        var updated = await repository.UpdateUserProfile(new UserProfile { UserId = "ana", Nick = "Anita", Lastname = "Mamani", Email = "anita@mail.bo" }, 1);
        var missing = await repository.UpdateUserProfile(new UserProfile { UserId = "ghost", Nick = "x" }, 1);

        Assert.Equal("Anita", updated.Nick);
        Assert.Null(missing);
        var stored = await seed.Db.UserProfiles.AsNoTracking().SingleAsync(profile => profile.UserId == "ana");
        Assert.Equal("anita@mail.bo", stored.Email);
        Assert.Equal("Mamani", stored.Lastname);
    }

    [Fact]
    public async Task ChangePassword_NewPasswordWorksForLogin()
    {
        using var seed = Users();
        var userService = new OnlineJudgeAdmin.Core.Application.Services.Implementations.UserService(Repository(seed), Mock.Of<IRoleRepository>(), Mock.Of<FluentValidation.IValidator<Problem>>());

        await userService.ChangePassword(new CurrentUser { UserId = "ana", SiteId = 1, Role = UserRolesEnum.Invitado }, "nueva-clave", "ana", 1);

        var login = await seed.PublicRepository().LoginAsync("ana", "nueva-clave", 1);
        Assert.Equal("ana", login.UserId);
        await Assert.ThrowsAsync<Exception>(() => Repository(seed).ChangePassword(new string('x', 32), "ghost", 1));
    }

    [Fact]
    public async Task DeleteRole_RemovesOnlyMatchingUserRoleAndSite()
    {
        using var seed = Users();
        seed.Db.Roles.AddRange(new DbRole { RoleId = 2, RoleName = "Docente" }, new DbRole { RoleId = 3, RoleName = "Auxiliar" });
        seed.Db.UserRoles.AddRange(
            new DbUserRole { UserId = "ana", RoleId = 2, SiteId = 1 },
            new DbUserRole { UserId = "ana", RoleId = 3, SiteId = 1 },
            new DbUserRole { UserId = "bob", RoleId = 2, SiteId = 2 });
        seed.Save();

        await Repository(seed).DeleteRoleAsync("ana", 2, 1);
        await Repository(seed).DeleteRoleAsync("bob", 2, 1);

        var remaining = await seed.Db.UserRoles.AsNoTracking().OrderBy(role => role.UserId).ToListAsync();
        Assert.Equal(new[] { ("ana", 3), ("bob", 2) }, remaining.Select(role => (role.UserId, role.RoleId)));
    }

    [Fact]
    public async Task DeleteUser_SoftDeactivatesOnSite()
    {
        using var seed = Users();

        await Repository(seed).DeleteUserAsync("ana", 1);
        await Repository(seed).DeleteUserAsync("ghost", 1);

        var ana = await seed.Db.Users.AsNoTracking().SingleAsync(user => user.UserId == "ana");
        Assert.False(ana.IsActive);
        Assert.False(ana.IsDeleted);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => seed.PublicRepository().LoginAsync("ana", "x", 1));
    }

    [Fact]
    public async Task GetAllTopics_IncludesClassifications()
    {
        using var seed = Users();
        var p = seed.Problem("A");
        seed.Tag("Grafos", p).Tag("DP", p);

        var topic = Assert.Single(await Repository(seed).GetAllTopicsAsync());

        Assert.Equal("Temas", topic.Name);
        Assert.Equal(2, topic.Classifications.Count);
    }
}
