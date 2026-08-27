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

        var profile = await service.UpdateUserProfile(User("admin", UserRolesEnum.Administrador), user, "old", 1);

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

        await service.ChangePassword(User("student", UserRolesEnum.Invitado), "secret1", "student", 1);

        Assert.False(string.IsNullOrWhiteSpace(capturedHash));
        Assert.NotEqual("secret1", capturedHash);
    }

    [Fact]
    public async Task ChangePassword_AllowsAuxiliaryToChangeAnotherUserPassword()
    {
        var userRepository = new Mock<IUserRepository>();
        var service = CreateService(userRepository.Object);

        await service.ChangePassword(User("assistant", UserRolesEnum.Auxiliar), "secret1", "student", 1);

        userRepository.Verify(item => item.ChangePassword(It.IsAny<string>(), "student", 1), Times.Once);
    }

    [Fact]
    public async Task DeleteRoleAsync_AllowsAdministrator()
    {
        var userRepository = new Mock<IUserRepository>();
        var service = CreateService(userRepository.Object);

        await service.DeleteRoleAsync(User("admin", UserRolesEnum.Administrador), "teacher", 2, 1);

        userRepository.Verify(item => item.DeleteRoleAsync("teacher", 2, 1), Times.Once);
    }

    [Fact]
    public async Task DeleteRoleAsync_RejectsNonPrivilegedRole()
    {
        var service = CreateService();

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteRoleAsync(User("teacher", UserRolesEnum.Docente), "student", 2, 1));

        Assert.Equal("Only administrators can delete roles.", error.Message);
    }

    [Fact]
    public async Task DeleteUserAsync_AllowsAdministrator()
    {
        var userRepository = new Mock<IUserRepository>();
        var service = CreateService(userRepository.Object);

        await service.DeleteUserAsync(User("admin", UserRolesEnum.Administrador), "student", 1);

        userRepository.Verify(item => item.DeleteUserAsync("student", 1), Times.Once);
    }

    [Fact]
    public async Task DeleteUserAsync_RejectsNonPrivilegedRole()
    {
        var service = CreateService();

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteUserAsync(User("teacher", UserRolesEnum.Docente), "student", 1));

        Assert.Equal("Only administrators can delete users.", error.Message);
    }

    [Fact]
    public async Task SearchUserProfilesAsync_AllowsTeacher()
    {
        var userRepository = new Mock<IUserRepository>();
        var service = CreateService(userRepository.Object);

        await service.SearchUserProfilesAsync(User("teacher", UserRolesEnum.Docente), "stu", 1);

        userRepository.Verify(item => item.SearchUserProfilesAsync("stu", 1), Times.Once);
    }

    [Fact]
    public async Task SearchUserProfilesAsync_RejectsNonPrivilegedRole()
    {
        var service = CreateService();

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SearchUserProfilesAsync(User("student", UserRolesEnum.Invitado), "stu", 1));

        Assert.Equal("Only administrators, assistants, or teachers can view users.", error.Message);
    }

    private static CurrentUser User(string userId, UserRolesEnum role) =>
        new() { UserId = userId, SiteId = 1, Role = role };

    private static UserService CreateService(IUserRepository? userRepository = null, IRoleRepository? roleRepository = null)
    {
        return new UserService(
            userRepository ?? Mock.Of<IUserRepository>(),
            roleRepository ?? Mock.Of<IRoleRepository>(),
            Mock.Of<IValidator<Problem>>());
    }
}
