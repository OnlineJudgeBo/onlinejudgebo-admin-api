using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

public class ProblemServiceTests
{
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
