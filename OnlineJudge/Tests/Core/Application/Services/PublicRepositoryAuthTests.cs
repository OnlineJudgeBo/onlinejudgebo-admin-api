using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

public class PublicRepositoryAuthTests
{
    private static string Md5Hex(string value) => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    // HUSTOJ salted format: base64(sha1(md5hex(password) + salt) + salt).
    private static string SaltedHash(string password, string salt = "ab12")
    {
        var digest = SHA1.HashData(Encoding.UTF8.GetBytes(Md5Hex(password) + salt));
        return Convert.ToBase64String(digest.Concat(Encoding.UTF8.GetBytes(salt)).ToArray());
    }

    private static JudgeSeed SeedUser(string password, string email = "Ana@Mail.bo", string role = null!)
    {
        var seed = new JudgeSeed();
        seed.Db.Users.Add(new DbUser { UserId = "ana", Password = password, Ip = "1.1.1.1", SiteId = 1, IsActive = true, IsDeleted = false });
        seed.Db.UserProfiles.Add(new DbUserProfile { UserId = "ana", SiteId = 1, Nick = "Ana", Lastname = "Pérez", Email = email });
        if (role != null)
        {
            seed.Db.Roles.Add(new DbRole { RoleId = 10, RoleName = role });
            seed.Db.UserRoles.Add(new DbUserRole { UserId = "ana", RoleId = 10, SiteId = 1 });
        }

        seed.Save();
        return seed;
    }

    [Fact]
    public async Task Login_AcceptsSaltedHashByUserIdOrCaseInsensitiveEmail()
    {
        using var seed = SeedUser(SaltedHash("secret1"));
        var repository = seed.PublicRepository();

        var byId = await repository.LoginAsync(" ana ", "secret1", 1);
        var byEmail = await repository.LoginAsync("ANA@mail.BO", "secret1", 1);

        Assert.Equal("ana", byId.UserId);
        Assert.Equal("Ana", byId.Nick);
        Assert.Equal("Pérez", byId.LastName);
        Assert.Equal("Ana@Mail.bo", byId.Email);
        Assert.Equal("Invitado", byId.Role);
        Assert.Equal("ana", byEmail.UserId);
    }

    [Fact]
    public async Task Login_AcceptsLegacyPlainMd5Hash()
    {
        using var seed = SeedUser(Md5Hex("secret1").ToUpperInvariant());

        Assert.Equal("ana", (await seed.PublicRepository().LoginAsync("ana", "secret1", 1)).UserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-base64-!!")]
    [InlineData("c2hvcnQ=")]
    public async Task Login_RejectsEmptyOrMalformedStoredHash(string storedHash)
    {
        using var seed = SeedUser(storedHash);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => seed.PublicRepository().LoginAsync("ana", "secret1", 1));
    }

    [Fact]
    public async Task Login_RejectsWrongPasswordOtherSiteAndUnknownUser()
    {
        using var seed = SeedUser(SaltedHash("secret1"));
        var repository = seed.PublicRepository();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.LoginAsync("ana", "secret2", 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.LoginAsync("ana", "secret1", 2));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.LoginAsync("ghost", "secret1", 1));
    }

    [Theory]
    [InlineData("Administrador", "Administrador")]
    [InlineData("super admin", "Administrador")]
    [InlineData("Auxiliar", "Auxiliar")]
    [InlineData("Docente", "Docente")]
    [InlineData("Invitado", "Invitado")]
    [InlineData("otro", "Invitado")]
    public async Task Login_ResolvesRoleName(string roleName, string expected)
    {
        using var seed = SeedUser(SaltedHash("secret1"), role: roleName);

        Assert.Equal(expected, (await seed.PublicRepository().LoginAsync("ana", "secret1", 1)).Role);
    }

    [Fact]
    public async Task GetAuthenticatedUser_ReturnsProfileAndRejectsInactive()
    {
        using var seed = SeedUser(SaltedHash("secret1"), role: "Docente");
        seed.Db.Users.Add(new DbUser { UserId = "off", Password = "x", Ip = "1", SiteId = 1, IsActive = false });
        seed.Save();
        var repository = seed.PublicRepository();

        var user = await repository.GetAuthenticatedUserAsync("ana", 1);

        Assert.Equal("Docente", user.Role);
        Assert.Equal("Ana", user.Nick);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.GetAuthenticatedUserAsync("off", 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.GetAuthenticatedUserAsync("ana", 2));
    }

    [Fact]
    public async Task GetAuthenticatedUser_WithoutProfileFallsBackToUserId()
    {
        using var seed = new JudgeSeed();
        seed.Db.Users.Add(new DbUser { UserId = "bare", Password = "x", Ip = "1", SiteId = 1, IsActive = true });
        seed.Save();

        var user = await seed.PublicRepository().GetAuthenticatedUserAsync("bare", 1);

        Assert.Equal("bare", user.Nick);
        Assert.Equal(string.Empty, user.Email);
    }

    [Fact]
    public async Task PasswordRecoveryTarget_MatchesEmailCaseInsensitivelyOnSite()
    {
        using var seed = SeedUser(SaltedHash("secret1"));
        var repository = seed.PublicRepository();

        var target = await repository.GetPasswordRecoveryTargetAsync("  ana@MAIL.bo ", 1);

        Assert.Equal("ana", target!.UserId);
        Assert.Equal("Ana", target.Nick);
        Assert.Null(await repository.GetPasswordRecoveryTargetAsync("ana@mail.bo", 2));
        Assert.Null(await repository.GetPasswordRecoveryTargetAsync("other@mail.bo", 1));
    }

    [Fact]
    public async Task ResetPassword_RequiresMatchingTokenEmailAndSite()
    {
        using var seed = SeedUser(SaltedHash("old"));
        var user = seed.Db.Users.Single();
        user.ResetPasswordToken = "hash";
        user.ResetPasswordExpires = DateTime.UtcNow.AddMinutes(10);
        seed.Save();
        var repository = seed.PublicRepository();

        Assert.False(await repository.ResetPasswordWithTokenAsync("ana@mail.bo", 1, "wrong", DateTime.UtcNow, "new"));
        Assert.False(await repository.ResetPasswordWithTokenAsync("other@mail.bo", 1, "hash", DateTime.UtcNow, "new"));
        Assert.False(await repository.ResetPasswordWithTokenAsync("ana@mail.bo", 2, "hash", DateTime.UtcNow, "new"));
        Assert.True(await repository.ResetPasswordWithTokenAsync(" ANA@mail.bo", 1, "hash", DateTime.UtcNow, "new"));
        Assert.False(await repository.ResetPasswordWithTokenAsync("ana@mail.bo", 1, "hash", DateTime.UtcNow, "again"));

        var stored = await seed.Db.Users.AsNoTracking().SingleAsync();
        Assert.Equal("new", stored.Password);
        Assert.Null(stored.ResetPasswordToken);
    }

    [Fact]
    public async Task Register_EmailIsUniqueAcrossAllSites()
    {
        // Documents current behavior: one email cannot be reused on another site.
        using var seed = SeedUser(SaltedHash("secret1"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => seed.PublicRepository().RegisterAsync("ana_site2", "hash", "ANA@mail.bo", null, null, null, 2, "1.1.1.1"));
    }
}
