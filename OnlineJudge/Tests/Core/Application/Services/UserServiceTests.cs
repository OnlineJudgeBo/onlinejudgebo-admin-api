using FluentValidation;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;

public class UserServiceTests
{
    [Fact]
    public async Task UpdateUserProfile_UpdatesUserWhenUserIdChangesThenUpdatesProfile()
    {
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(item => item.UpdateUserProfile(It.IsAny<UserProfile>(), 1)).ReturnsAsync(new UserProfile { UserId = "new" });
        var service = CreateService(userRepository.Object);
        var user = new User { UserId = "new", UserProfile = new UserProfile { UserId = "new" } };

        var profile = await service.UpdateUserProfile(user, "old", 1);

        Assert.Equal("new", profile.UserId);
        userRepository.Verify(item => item.UpdateUser(user, "old", 1), Times.Once);
        userRepository.Verify(item => item.UpdateUserProfile(user.UserProfile, 1), Times.Once);
    }

    [Fact]
    public async Task ChangePassword_HashesPasswordBeforeSaving()
    {
        string? capturedHash = null;
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(item => item.ChangePassword(It.IsAny<string>(), "student", 1))
            .Callback<string, string, int>((hash, _, _) => capturedHash = hash)
            .Returns(Task.CompletedTask);
        var service = CreateService(userRepository.Object);

        await service.ChangePassword("secret1", "student", 1);

        Assert.False(string.IsNullOrWhiteSpace(capturedHash));
        Assert.NotEqual("secret1", capturedHash);
    }

    [Theory]
    [InlineData("Administrador")]
    [InlineData("Docente")]
    public async Task DeleteRoleAsync_AllowsPrivilegedRoles(string roleName)
    {
        var userRepository = new Mock<IUserRepository>();
        var roleRepository = new Mock<IRoleRepository>();
        roleRepository.Setup(item => item.GetUserRoleAsync("teacher", 1)).ReturnsAsync(new UserRole { Role = new Role { RoleName = roleName } });
        var service = CreateService(userRepository.Object, roleRepository.Object);

        await service.DeleteRoleAsync("teacher", 2, 1);

        userRepository.Verify(item => item.DeleteRoleAsync("teacher", 2, 1), Times.Once);
    }

    [Fact]
    public async Task DeleteRoleAsync_RejectsNonPrivilegedRole()
    {
        var roleRepository = new Mock<IRoleRepository>();
        roleRepository.Setup(item => item.GetUserRoleAsync("student", 1)).ReturnsAsync(new UserRole { Role = new Role { RoleName = "Invitado" } });
        var service = CreateService(roleRepository: roleRepository.Object);

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteRoleAsync("student", 2, 1));

        Assert.Equal("Solo los administradores pueden eliminar roles.", error.Message);
    }

    [Fact]
    public async Task DeleteUserAsync_AllowsWhenRepositoryReturnsNullRole()
    {
        var userRepository = new Mock<IUserRepository>();
        var roleRepository = new Mock<IRoleRepository>();
        roleRepository.Setup(item => item.GetUserRoleAsync("student", 1)).ReturnsAsync((UserRole)null!);
        var service = CreateService(userRepository.Object, roleRepository.Object);

        await service.DeleteUserAsync(new CurrentUser { UserId = "admin", SiteId = 1, Role = UserRolesEnum.Administrador }, "student", 1);

        userRepository.Verify(item => item.DeleteUserAsync("student", 1), Times.Once);
    }

    [Fact]
    public async Task DeleteUserAsync_RejectsNonPrivilegedRole()
    {
        var roleRepository = new Mock<IRoleRepository>();
        roleRepository.Setup(item => item.GetUserRoleAsync("student", 1)).ReturnsAsync(new UserRole { Role = new Role { RoleName = "Invitado" } });
        var service = CreateService(roleRepository: roleRepository.Object);

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteUserAsync(new CurrentUser(), "student", 1));

        Assert.Equal("Solo los administradores pueden eliminar usuarios.", error.Message);
    }

    private static UserService CreateService(IUserRepository? userRepository = null, IRoleRepository? roleRepository = null)
    {
        return new UserService(
            userRepository ?? Mock.Of<IUserRepository>(),
            roleRepository ?? Mock.Of<IRoleRepository>(),
            Mock.Of<IValidator<Problem>>());
    }
}
