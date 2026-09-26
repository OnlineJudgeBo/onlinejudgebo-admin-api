using FluentValidation;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;

public class ScheduleServiceCoverageTests
{
    private readonly Mock<IScheduleRepository> _repository = new();
    private ScheduleService Service => new(_repository.Object);

    [Fact]
    public void Constructor_RejectsNullRepository()
    {
        Assert.Throws<ArgumentNullException>(() => new ScheduleService(null!));
    }

    [Fact]
    public async Task PassThroughOperations_DelegateToRepository()
    {
        var subject = new Subject { Name = "Algoritmos" };
        var service = Service;

        await service.GetSchedulesWithTeachersAsync();
        await service.GetSchedulesAsync();
        await service.DeleteScheduleAsync(3);
        await service.CreateTeacherAsync("Ana");
        await service.UpdateTeacherAsync(4, "Bea");
        await service.DeleteTeacherAsync(4);
        await service.GetTeachersAsync();
        await service.CreateSubjectAsync(subject);
        await service.UpdateSubjectAsync(5, subject);
        await service.DeleteSubjectAsync(5);
        await service.GetSubjectsAsync();
        await service.GetSubjectAssistantBySubjectIdAsync(5);
        await service.UpsertSubjectAssistantAsync(5, "Aux", "Lun 10:00");

        _repository.Verify(item => item.GetSchedulesWithTeachersAsync(), Times.Once);
        _repository.Verify(item => item.GetSchedulesAsync(), Times.Once);
        _repository.Verify(item => item.DeleteScheduleAsync(3), Times.Once);
        _repository.Verify(item => item.CreateTeacherAsync("Ana"), Times.Once);
        _repository.Verify(item => item.UpdateTeacherAsync(4, "Bea"), Times.Once);
        _repository.Verify(item => item.DeleteTeacherAsync(4), Times.Once);
        _repository.Verify(item => item.GetTeachersAsync(), Times.Once);
        _repository.Verify(item => item.CreateSubjectAsync(subject), Times.Once);
        _repository.Verify(item => item.UpdateSubjectAsync(5, subject), Times.Once);
        _repository.Verify(item => item.DeleteSubjectAsync(5), Times.Once);
        _repository.Verify(item => item.GetSubjectsAsync(), Times.Once);
        _repository.Verify(item => item.GetSubjectAssistantBySubjectIdAsync(5), Times.Once);
        _repository.Verify(item => item.UpsertSubjectAssistantAsync(5, "Aux", "Lun 10:00"), Times.Once);
    }

    [Fact]
    public async Task UpdateScheduleAsync_BuildsTwoHourBlockWithIdAndDay()
    {
        var teacher = new Teacher { Id = 1, Name = "Ana" };
        var subject = new Subject { Id = 2, Name = "Algoritmos" };
        _repository.Setup(item => item.GetTeacherByIdAsync(1)).ReturnsAsync(teacher);
        _repository.Setup(item => item.GetSubjectByIdAsync(2)).ReturnsAsync(subject);
        Schedule? saved = null;
        _repository
            .Setup(item => item.UpdateScheduleAsync(9, It.IsAny<Schedule>()))
            .Callback<int, Schedule>((_, schedule) => saved = schedule)
            .ReturnsAsync((int _, Schedule schedule) => schedule);

        await Service.UpdateScheduleAsync(9, Request("Martes"));

        Assert.NotNull(saved);
        Assert.Equal(9, saved!.Id);
        Assert.Equal("Martes", saved.DayOfWeek);
        Assert.Equal(new TimeOnly(14, 30), saved.StartTime);
        Assert.Equal(new TimeOnly(16, 30), saved.EndTime);
        Assert.Same(teacher, saved.Teacher);
        Assert.Same(subject, saved.Subject);
    }

    [Fact]
    public async Task CreateAndUpdate_RejectEmptyDays()
    {
        var create = await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateScheduleAsync(Request()));
        var update = await Assert.ThrowsAsync<ArgumentException>(() => Service.UpdateScheduleAsync(1, Request()));

        Assert.Equal("Debe enviar al menos un día para crear un horario.", create.Message);
        Assert.Equal("Debe enviar al menos un día para crear un horario.", update.Message);
    }

    [Fact]
    public async Task CreateScheduleAsync_RejectsMissingSubject()
    {
        _repository.Setup(item => item.GetTeacherByIdAsync(1)).ReturnsAsync(new Teacher { Id = 1, Name = "Ana" });

        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateScheduleAsync(Request("Lunes")));

        Assert.Equal("La materia no existe.", error.Message);
        _repository.Verify(item => item.CreateScheduleAsync(It.IsAny<Schedule>()), Times.Never);
    }

    [Fact]
    public async Task CreateScheduleAsync_RejectsInvalidTime()
    {
        _repository.Setup(item => item.GetTeacherByIdAsync(1)).ReturnsAsync(new Teacher { Id = 1, Name = "Ana" });
        _repository.Setup(item => item.GetSubjectByIdAsync(2)).ReturnsAsync(new Subject { Id = 2, Name = "Algoritmos" });
        var request = Request("Lunes");
        request.ScheduleTime = "25:99";

        await Assert.ThrowsAsync<FormatException>(() => Service.CreateScheduleAsync(request));
    }

    private static ScheduleForCreationModel Request(params string[] days) => new()
    {
        ScheduleDays = days.ToList(),
        ScheduleTime = "14:30",
        Teacher = 1,
        Subject = 2
    };
}

public class UserServiceCoverageTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRoleRepository> _roles = new();
    private UserService Service => new(_users.Object, _roles.Object, Mock.Of<IValidator<Problem>>());

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new UserService(null!, _roles.Object, Mock.Of<IValidator<Problem>>()));
        Assert.Throws<ArgumentNullException>(() => new UserService(_users.Object, null!, Mock.Of<IValidator<Problem>>()));
        Assert.Throws<ArgumentNullException>(() => new UserService(_users.Object, _roles.Object, null!));
    }

    [Theory]
    [InlineData(UserRolesEnum.Administrador)]
    [InlineData(UserRolesEnum.Auxiliar)]
    [InlineData(UserRolesEnum.Docente)]
    public async Task GetAllUserProfilesAsync_AllowsUserManagers(UserRolesEnum role)
    {
        await Service.GetAllUserProfilesAsync(User("manager", role), 1);

        _users.Verify(item => item.GetAllUsersProfilesAsync(1), Times.Once);
    }

    [Fact]
    public async Task GetAllUserProfilesAsync_RejectsGuestsAndOtherSites()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service.GetAllUserProfilesAsync(User("guest", UserRolesEnum.Invitado), 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service.GetAllUserProfilesAsync(User("admin", UserRolesEnum.Administrador), 2));
        _users.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AvailabilityChecks_Delegate()
    {
        var profile = new UserProfile { UserId = "ana", Email = "a@b.c" };
        _users.Setup(item => item.CheckUsernameAvailable(profile, 1)).ReturnsAsync(true);
        _users.Setup(item => item.CheckUserEmailAvailable(profile, 1)).ReturnsAsync(false);

        Assert.True(await Service.CheckUsernameAvailable(profile, 1));
        Assert.False(await Service.CheckUserEmailAvailable(profile, 1));
    }

    [Fact]
    public async Task UpdateUserProfile_SameUserIdOnlyUpdatesProfile()
    {
        var user = new User { UserId = "ana", UserProfile = new UserProfile { Nick = "Ana" } };

        await Service.UpdateUserProfile(User("ana", UserRolesEnum.Invitado), user, "ana", 1);

        _users.Verify(item => item.UpdateUser(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        _users.Verify(item => item.UpdateUserProfile(It.Is<UserProfile>(p => p.UserId == "ana"), 1), Times.Once);
    }

    [Fact]
    public async Task UpdateUserProfile_TrimsNewUserIdBeforeComparing()
    {
        var user = new User { UserId = "  ana  ", UserProfile = new UserProfile() };

        await Service.UpdateUserProfile(User("ana", UserRolesEnum.Invitado), user, "ana", 1);

        Assert.Equal("ana", user.UserId);
        _users.Verify(item => item.UpdateUser(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserProfile_SelfRenameIsAllowedForGuests()
    {
        var user = new User { UserId = "ana_2", UserProfile = new UserProfile() };

        await Service.UpdateUserProfile(User("ana", UserRolesEnum.Invitado), user, "ana", 1);

        _users.Verify(item => item.UpdateUser(user, "ana", 1), Times.Once);
        Assert.Equal("ana_2", user.UserProfile.UserId);
    }

    [Theory]
    [InlineData(UserRolesEnum.Invitado)]
    [InlineData(UserRolesEnum.Docente)]
    public async Task UpdateUserProfile_RejectsEditingSomeoneElseWithoutPrivilege(UserRolesEnum role)
    {
        var user = new User { UserId = "bob", UserProfile = new UserProfile() };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service.UpdateUserProfile(User("ana", role), user, "bob", 1));
        _users.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateUserProfile_OwnerMatchIsCaseSensitive()
    {
        var user = new User { UserId = "Ana", UserProfile = new UserProfile() };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service.UpdateUserProfile(User("ana", UserRolesEnum.Invitado), user, "Ana", 1));
    }

    [Fact]
    public async Task UpdateUserProfile_RejectsNullOrBlankNewUserId()
    {
        var user = new User { UserId = null!, UserProfile = new UserProfile() };

        await Assert.ThrowsAsync<ArgumentException>(() => Service.UpdateUserProfile(User("admin", UserRolesEnum.Administrador), user, "ana", 1));
    }

    [Fact]
    public async Task UpdateUserProfile_RejectsOtherSite()
    {
        var user = new User { UserId = "ana", UserProfile = new UserProfile() };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service.UpdateUserProfile(User("admin", UserRolesEnum.Administrador), user, "ana", 2));
    }

    [Fact]
    public async Task ChangePassword_RejectsOtherUserWithoutPrivilegeAndOtherSite()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service.ChangePassword(User("ana", UserRolesEnum.Docente), "secret1", "bob", 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service.ChangePassword(User("ana", UserRolesEnum.Invitado), "secret1", "ana", 2));
        _users.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ChangePassword_AllowsSelf()
    {
        await Service.ChangePassword(User("ana", UserRolesEnum.Invitado), "secret1", "ana", 1);

        _users.Verify(item => item.ChangePassword(It.Is<string>(hash => hash != "secret1"), "ana", 1), Times.Once);
    }

    [Theory]
    [InlineData(UserRolesEnum.Auxiliar)]
    [InlineData(UserRolesEnum.Docente)]
    public async Task DeleteRoleAndUser_OnlyAdministrators(UserRolesEnum role)
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service.DeleteRoleAsync(User("x", role), "bob", 2, 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service.DeleteUserAsync(User("x", role), "bob", 1));
        _users.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteRoleAndUser_RejectOtherSite()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service.DeleteRoleAsync(User("admin", UserRolesEnum.Administrador), "bob", 2, 3));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service.DeleteUserAsync(User("admin", UserRolesEnum.Administrador), "bob", 3));
    }

    [Fact]
    public async Task EnsureSameSite_RejectsNullUserAndNonPositiveSite()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Service.DeleteUserAsync(null!, "bob", 1));
        var zeroSite = new CurrentUser { UserId = "admin", SiteId = 0, Role = UserRolesEnum.Administrador };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service.DeleteUserAsync(zeroSite, "bob", 0));
    }

    private static CurrentUser User(string userId, UserRolesEnum role) => new() { UserId = userId, SiteId = 1, Role = role };
}
