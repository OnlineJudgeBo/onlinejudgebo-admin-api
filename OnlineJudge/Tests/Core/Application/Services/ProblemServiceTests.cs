using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

public class ProblemServiceTests
{
    private readonly Mock<IProblemRepository> mockProblemRepo = new Mock<IProblemRepository>();
    private readonly Mock<ITopicRepository> mockTopicRepo = new Mock<ITopicRepository>();
    private readonly Mock<IPrivilegeRepository> mockPrivilegeRepo = new Mock<IPrivilegeRepository>();
    private readonly Mock<IFileSystemLocalManagerManager> mockFileSystemManager = new Mock<IFileSystemLocalManagerManager>();
    private readonly Mock<IValidator<Problem>> mockValidator = new Mock<IValidator<Problem>>();
    private readonly ProblemService service;

    public ProblemServiceTests()
    {
        service = new ProblemService(
            mockProblemRepo.Object,
            mockTopicRepo.Object,
            mockPrivilegeRepo.Object,
            mockFileSystemManager.Object,
            mockValidator.Object);
    }

    [Fact]
    public async Task GetAllProblemsAsync_Admin_ReturnsAllProblems()
    {
        // Arrange
        var problems = new List<Problem> { new Problem(), new Problem() };
        //mockProblemRepo.Setup(x => x.GetAllProblemsForAdminAsync()).ReturnsAsync(problems);
        var currentUser = new CurrentUser { Role = UserRolesEnum.Administrador };

        // Act
        var result = await service.GetAllProblemsAsync(currentUser);

        // Assert
        Assert.Equal(problems.Count, result.Count());
        //mockProblemRepo.Verify(x => x.GetAllProblemsForAdminAsync(), Times.Once);
    }

    [Fact]
    public async Task GetAllProblemsAsync_NoAdmin_ReturnsFilteredProblems()
    {
        // Arrange
        var problems = new List<Problem> { new Problem(), new Problem() };
        //mockProblemRepo.Setup(x => x.GetAllProblemsAsync()).ReturnsAsync(problems);
        var currentUser = new CurrentUser { Role = UserRolesEnum.Auxiliar };

        // Act
        var result = await service.GetAllProblemsAsync(currentUser);

        // Assert
        Assert.Equal(problems.Count, result.Count());
        //mockProblemRepo.Verify(x => x.GetAllProblemsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetProblemByIdAsync_ReturnsProblem()
    {
        // Arrange
        int problemId = 1;
        var expectedProblem = new Problem { ProblemId = problemId };
        mockProblemRepo.Setup(x => x.GetProblemByIdAsync(problemId)).ReturnsAsync(expectedProblem);

        // Act
        var result = await service.GetProblemByIdAsync(problemId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedProblem.ProblemId, result.ProblemId);
        mockProblemRepo.Verify(x => x.GetProblemByIdAsync(problemId), Times.Once);
    }

    [Fact]
    public async Task CreateProblemAsync_ValidatesAndCreatesProblem()
    {
        // Arrange
        //string userId = "user123";
        //var problem = new Problem { Title = "New Problem" };
        //var createdProblem = new Problem { ProblemId = 1, Title = "New Problem" };
        //mockValidator.Setup(v => v.ValidateAsync(problem, default)).ReturnsAsync(new FluentValidation.Results.ValidationResult());
        //mockProblemRepo.Setup(x => x.CreateProblemAsync(problem)).ReturnsAsync(createdProblem);
        //mockFileSystemManager.Setup(x => x.CreateFolder(It.IsAny<string>()));
        //mockFileSystemManager.Setup(x => x.WriteToFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));

        // Act
        //var result = await service.CreateProblemAsync(userId, problem);

        // Assert
        //Assert.NotNull(result);
        //Assert.Equal(createdProblem.ProblemId, result.ProblemId);
        //mockProblemRepo.Verify(x => x.CreateProblemAsync(It.IsAny<Problem>()), Times.Once);
        //mockFileSystemManager.Verify(x => x.CreateFolder(It.IsAny<string>()), Times.Once);
        //mockFileSystemManager.Verify(x => x.WriteToFile(It.IsAny<string>(), "sample.in", problem.SampleInput), Times.Once);
    }
}
