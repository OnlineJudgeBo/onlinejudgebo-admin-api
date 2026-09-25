using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.Controllers;
using OnlineJudgeAdminApi.DataTransferObjects;
using static ControllerTestSupport;

public class UsersControllerTests
{
    private readonly Mock<IUserService> _service = new();

    private UsersController Controller(string userId = "admin", int siteId = 1, string role = "Administrador")
    {
        var context = AuthenticatedContext(userId, siteId, role);
        return new UsersController(_service.Object, Claims(context), ApiMapper).WithContext(context);
    }

    [Fact]
    public async Task GetAll_WithoutSearchTermListsSiteUsers()
    {
        var users = new[] { new User { UserId = "ana" } };
        _service.Setup(item => item.GetAllUserProfilesAsync(It.Is<CurrentUser>(u => u.UserId == "admin"), 2)).ReturnsAsync(users);

        var result = await Controller(siteId: 2).GetAllUserProfilesAsync("  ");

        Assert.Same(users, OkValue(result));
        _service.Verify(item => item.SearchUserProfilesAsync(It.IsAny<CurrentUser>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetAll_WithSearchTermSearchesTrimmedTerm()
    {
        await Controller().GetAllUserProfilesAsync("  ana  ");

        _service.Verify(item => item.SearchUserProfilesAsync(It.IsAny<CurrentUser>(), "ana", 1), Times.Once);
    }

    [Fact]
    public async Task AvailabilityChecks_MapRequestAndUseSite()
    {
        _service.Setup(item => item.CheckUsernameAvailable(It.Is<UserProfile>(p => p.UserId == "new_user"), 3)).ReturnsAsync(true);
        _service.Setup(item => item.CheckUserEmailAvailable(It.Is<UserProfile>(p => p.Email == "a@b.c"), 3)).ReturnsAsync(false);
        var controller = Controller(siteId: 3);

        Assert.Equal(true, OkValue(await controller.CheckUsernameAvailable(new UserAvailableForRequest { UserId = "new_user", Email = "" })));
        Assert.Equal(false, OkValue(await controller.CheckUserEmailAvailable(new UserAvailableForRequest { UserId = "", Email = "a@b.c" })));
    }

    [Fact]
    public async Task UpdateProfile_MapsDtoToUserAndProfile()
    {
        User? captured = null;
        _service
            .Setup(item => item.UpdateUserProfile(It.IsAny<CurrentUser>(), It.IsAny<User>(), "old", 1))
            .Callback<CurrentUser, User, string, int>((_, user, _, _) => captured = user)
            .ReturnsAsync(new UserProfile { UserId = "new" });

        var result = await Controller().UpdateProfileUser(new UserForUpdate { UserName = "new", Email = "a@b.c", Name = "Ana", LastName = "Pérez" }, "old");

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("new", captured!.UserId);
        Assert.Equal("new", captured.UserProfile.UserId);
        Assert.Equal("a@b.c", captured.UserProfile.Email);
        Assert.Equal("Ana", captured.UserProfile.Nick);
        Assert.Equal("Pérez", captured.UserProfile.Lastname);
    }

    [Fact]
    public async Task ChangePasswordDeleteRoleAndDeleteUser_PassCurrentUserAndSite()
    {
        var controller = Controller(userId: "boss", siteId: 4);

        Assert.IsType<OkResult>(await controller.ChangePassword(new UserPasswordForUpdate { Password = "secret1" }, "ana"));
        Assert.IsType<OkResult>(await controller.DeleteRole("ana", 2));
        Assert.IsType<OkResult>(await controller.DeleteUser("ana"));

        _service.Verify(item => item.ChangePassword(It.Is<CurrentUser>(u => u.UserId == "boss" && u.SiteId == 4), "secret1", "ana", 4), Times.Once);
        _service.Verify(item => item.DeleteRoleAsync(It.Is<CurrentUser>(u => u.UserId == "boss"), "ana", 2, 4), Times.Once);
        _service.Verify(item => item.DeleteUserAsync(It.Is<CurrentUser>(u => u.UserId == "boss"), "ana", 4), Times.Once);
    }

    [Fact]
    public void Constructor_RequiresAuthenticatedUser()
    {
        var anonymous = new Microsoft.AspNetCore.Http.DefaultHttpContext();

        Assert.Throws<UnauthorizedAccessException>(() => new UsersController(_service.Object, Claims(anonymous), ApiMapper));
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var claims = Claims(AuthenticatedContext());
        Assert.Throws<ArgumentNullException>(() => new UsersController(null!, claims, ApiMapper));
        Assert.Throws<ArgumentNullException>(() => new UsersController(_service.Object, null!, ApiMapper));
        Assert.Throws<ArgumentNullException>(() => new UsersController(_service.Object, claims, null!));
    }
}

public class RolesControllerTests
{
    [Fact]
    public async Task Actions_UseSiteFromToken()
    {
        var service = new Mock<IRoleService>();
        var context = AuthenticatedContext("admin", 5, "Administrador");
        var controller = new RolesController(service.Object, Claims(context), ApiMapper);

        Assert.IsType<OkObjectResult>(await controller.GetUserRolesAsync());
        Assert.IsType<OkObjectResult>(await controller.GetAllRolesAsync());
        Assert.IsType<OkResult>(await controller.AddRoleToUserAsync("ana", 2));
        Assert.IsType<OkResult>(await controller.RemoveRoleFromUserAsync("ana", 2));

        service.Verify(item => item.GetUserRolesAsync(5), Times.Once);
        service.Verify(item => item.GetAllRolesAsync(), Times.Once);
        service.Verify(item => item.AddRoleToUserAsync("ana", 2, 5), Times.Once);
        service.Verify(item => item.RemoveRoleFromUserAsync("ana", 2, 5), Times.Once);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var claims = Claims(AuthenticatedContext());
        Assert.Throws<ArgumentNullException>(() => new RolesController(null!, claims, ApiMapper));
        Assert.Throws<ArgumentNullException>(() => new RolesController(Mock.Of<IRoleService>(), claims, null!));
        Assert.Throws<ArgumentNullException>(() => new RolesController(Mock.Of<IRoleService>(), null!, ApiMapper));
    }
}

public class TopicsControllerTests
{
    private readonly Mock<ITopicService> _service = new();
    private TopicsController Controller => new(_service.Object, ApiMapper);

    [Fact]
    public async Task GetAll_ReturnsTopics()
    {
        var topics = new[] { new Topic { Name = "Grafos" } };
        _service.Setup(item => item.GetAllTopicsAsync()).ReturnsAsync(topics);

        Assert.Same(topics, OkValue(await Controller.GetAllTopicsAsync()));
    }

    [Fact]
    public async Task AddTopic_MapsNameAndReturnsRefreshedList()
    {
        await Controller.AddTopicAsync(new TopicForCreation { Name = "DP" });

        _service.Verify(item => item.AddTopicAsync(It.Is<Topic>(t => t.Name == "DP")), Times.Once);
        _service.Verify(item => item.GetAllTopicsAsync(), Times.Once);
    }

    [Fact]
    public async Task AddClassification_ReturnsCreated()
    {
        var dto = new TopicAddClassificationForCreation { Classifications = new List<ClassificationAddToTopic> { new() { Name = "BFS" } } };

        var result = await Controller.AddClassificationToTopic(7, dto);

        Assert.IsType<CreatedResult>(result);
        _service.Verify(item => item.AddClassificationToTopic(7, It.Is<Topic>(t => t.Classifications.Single().Name == "BFS")), Times.Once);
    }

    [Fact]
    public async Task UpdateClassification_ReturnsNoContent()
    {
        var result = await Controller.UpdateClassificationFromTopic(new ClassificationForUpdate { Name = "DFS" }, 3);

        Assert.IsType<NoContentResult>(result);
        _service.Verify(item => item.UpdateClassification(It.Is<Classification>(c => c.Name == "DFS"), 3), Times.Once);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new TopicsController(null!, ApiMapper));
        Assert.Throws<ArgumentNullException>(() => new TopicsController(_service.Object, null!));
    }
}

public class ContestsControllerTests
{
    private readonly Mock<IContestService> _service = new();

    private ContestsController Controller(string userId = "teacher", int siteId = 1)
    {
        var context = AuthenticatedContext(userId, siteId, "Docente");
        return new ContestsController(_service.Object, Claims(context), ApiMapper).WithContext(context);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAll_PassesPromotedFlag(bool includePromoted)
    {
        await Controller().GetAllContestAsync(includePromoted);

        _service.Verify(item => item.GetAllContestAsync(It.Is<CurrentUser>(u => u.UserId == "teacher"), includePromoted), Times.Once);
    }

    [Fact]
    public async Task GetById_UsesSite()
    {
        await Controller(siteId: 3).GetContestById(10);

        _service.Verify(item => item.GetContestById(10, 3), Times.Once);
    }

    [Fact]
    public async Task Create_UsesTokenUserAsCreatorAndPassesManualUsers()
    {
        var dto = ContestDto<ContestForCreation>();
        dto.ManualUserList = "ana\nbob";

        await Controller("creator", 2).CreateContestAsync(dto);

        _service.Verify(item => item.CreateContestAsync("creator", It.Is<Contest>(c => c.Title == "Final"), "ana\nbob", 2), Times.Once);
    }

    [Fact]
    public async Task Update_ForcesRouteContestId()
    {
        var dto = ContestDto<ContestForUpdate>();

        await Controller().UpdateContestAsync(42, dto);

        _service.Verify(item => item.UpdateContestAsync(42, It.Is<Contest>(c => c.ContestId == 42 && c.Title == "Final"), null, 1), Times.Once);
    }

    [Fact]
    public async Task Promote_Delegates()
    {
        Assert.IsType<OkResult>(await Controller(siteId: 2).PromoteContestAsync(42));

        _service.Verify(item => item.PromoteContestAsync(42, 2), Times.Once);
    }

    private static T ContestDto<T>() where T : new()
    {
        dynamic dto = new T();
        dto.Title = "Final";
        dto.Description = "d";
        dto.StartDate = new DateTime(2026, 1, 1);
        dto.EndDate = new DateTime(2026, 1, 2);
        dto.SelectedProblem = new List<ProblemForContestCreation>();
        dto.SelectedUser = new List<UserForContestCreation>();
        dto.selectedLanguages = new List<ProgrammingLanguageForContestCreation>();
        return (T)dto;
    }
}

public class ScheduleControllersTests
{
    private readonly Mock<IScheduleService> _service = new();

    [Fact]
    public async Task ScheduleController_DelegatesAndMapsRequests()
    {
        var controller = new ScheduleController(_service.Object, ApiMapper);
        var dto = new ScheduleForCreation { ScheduleDays = new List<string> { "Lunes", "Martes" }, ScheduleTime = "08:00", Subject = 2, Teacher = 3 };

        Assert.IsType<OkObjectResult>(await controller.GetSchedules());
        Assert.IsType<OkObjectResult>(await controller.GetSchedulesWithTeachers());
        Assert.IsType<OkObjectResult>(await controller.UpdateSchedule(9, dto));
        Assert.IsType<OkResult>(await controller.DeleteSchedule(9));
        Assert.Equal("Horarios creados correctamente.", OkValue(await controller.CreateSchedule(dto)));

        _service.Verify(item => item.GetSchedulesAsync(), Times.Once);
        _service.Verify(item => item.GetSchedulesWithTeachersAsync(), Times.Once);
        _service.Verify(item => item.UpdateScheduleAsync(9, It.Is<ScheduleForCreationModel>(m => m.ScheduleTime == "08:00" && m.Subject == 2 && m.Teacher == 3 && m.ScheduleDays.Count == 2)), Times.Once);
        _service.Verify(item => item.DeleteScheduleAsync(9), Times.Once);
        _service.Verify(item => item.CreateScheduleAsync(It.Is<ScheduleForCreationModel>(m => m.ScheduleDays.SequenceEqual(new[] { "Lunes", "Martes" }))), Times.Once);
    }

    [Fact]
    public async Task SubjectsController_DelegatesWithSubjectName()
    {
        var controller = new SubjectsController(_service.Object);

        Assert.IsType<OkObjectResult>(await controller.CreateSubject(new SubjectForCreation { SubjectName = "Algoritmos" }));
        Assert.IsType<OkObjectResult>(await controller.UpdateSubject(4, new SubjectForCreation { SubjectName = "Grafos" }));
        Assert.IsType<OkResult>(await controller.DeleteSubject(4));
        Assert.IsType<OkObjectResult>(await controller.GetSubjects());

        _service.Verify(item => item.CreateSubjectAsync(It.Is<Subject>(s => s.Name == "Algoritmos")), Times.Once);
        _service.Verify(item => item.UpdateSubjectAsync(4, It.Is<Subject>(s => s.Name == "Grafos")), Times.Once);
        _service.Verify(item => item.DeleteSubjectAsync(4), Times.Once);
        _service.Verify(item => item.GetSubjectsAsync(), Times.Once);
    }

    [Fact]
    public async Task TeachersController_DelegatesWithTeacherName()
    {
        var controller = new TeachersController(_service.Object);

        Assert.IsType<OkObjectResult>(await controller.CreateTeacher(new TeacherScheduleForCreation { TeacherName = "Ana" }));
        Assert.IsType<OkObjectResult>(await controller.UpdateTeacher(4, new TeacherScheduleForCreation { TeacherName = "Bea" }));
        Assert.IsType<OkResult>(await controller.DeleteTeacher(4));
        Assert.IsType<OkObjectResult>(await controller.GetTeachers());

        _service.Verify(item => item.CreateTeacherAsync("Ana"), Times.Once);
        _service.Verify(item => item.UpdateTeacherAsync(4, "Bea"), Times.Once);
        _service.Verify(item => item.DeleteTeacherAsync(4), Times.Once);
        _service.Verify(item => item.GetTeachersAsync(), Times.Once);
    }

    [Fact]
    public async Task SubjectAssistantsController_Delegates()
    {
        var controller = new SubjectAssistantsController(_service.Object);

        Assert.IsType<OkObjectResult>(await controller.GetSubjectAssistant(4));
        Assert.IsType<OkObjectResult>(await controller.UpsertSubjectAssistant(4, new SubjectAssistantForCreation { Name = "Aux", Schedule = "Lun" }));

        _service.Verify(item => item.GetSubjectAssistantBySubjectIdAsync(4), Times.Once);
        _service.Verify(item => item.UpsertSubjectAssistantAsync(4, "Aux", "Lun"), Times.Once);
    }

    [Fact]
    public void Constructors_RejectNullService()
    {
        Assert.Throws<ArgumentNullException>(() => new ScheduleController(null!, ApiMapper));
        Assert.Throws<ArgumentNullException>(() => new ScheduleController(_service.Object, null!));
        Assert.Throws<ArgumentNullException>(() => new SubjectsController(null!));
        Assert.Throws<ArgumentNullException>(() => new TeachersController(null!));
        Assert.Throws<ArgumentNullException>(() => new SubjectAssistantsController(null!));
    }
}

public class StaticsAndLanguagesControllersTests
{
    [Fact]
    public async Task StaticsController_UsesSiteFromToken()
    {
        var service = new Mock<IStatiscService>();
        var context = AuthenticatedContext("ana", 6);
        var controller = new StaticsController(service.Object, Claims(context), ApiMapper);

        Assert.IsType<OkObjectResult>(await controller.GetLast365DaysSubmissionsByMonthAsync());
        Assert.IsType<OkObjectResult>(await controller.GetSubmissionsByLanguageAsync());

        service.Verify(item => item.GetLast365DaysSubmissionsByMonthAsync(6), Times.Once);
        service.Verify(item => item.GetSubmissionsByLanguageAsync(6), Times.Once);
    }

    [Fact]
    public async Task ProgrammingLanguagesController_ReturnsLanguages()
    {
        var service = new Mock<IProgrammingLanguageService>();
        var controller = new ProgrammingLanguagesController(service.Object, ApiMapper);

        Assert.IsType<OkObjectResult>(await controller.GetAllContestAsync());

        service.Verify(item => item.GetAllProgrammingLanguageAsync(), Times.Once);
    }

    [Fact]
    public void Constructors_RejectNullDependencies()
    {
        var claims = Claims(AuthenticatedContext());
        Assert.Throws<ArgumentNullException>(() => new StaticsController(null!, claims, ApiMapper));
        Assert.Throws<ArgumentNullException>(() => new StaticsController(Mock.Of<IStatiscService>(), claims, null!));
        Assert.Throws<ArgumentNullException>(() => new StaticsController(Mock.Of<IStatiscService>(), null!, ApiMapper));
        Assert.Throws<ArgumentNullException>(() => new ProgrammingLanguagesController(null!, ApiMapper));
        Assert.Throws<ArgumentNullException>(() => new ProgrammingLanguagesController(Mock.Of<IProgrammingLanguageService>(), null!));
    }
}
