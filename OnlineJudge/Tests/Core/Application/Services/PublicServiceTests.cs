using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

public class PublicServiceTests
{
    [Fact]
    public async Task SubmitAsync_AllowsContestNumZeroWithoutProblemId()
    {
        PublicSubmissionRequest? capturedRequest = null;
        Mock<IPublicRepository> publicRepository = new Mock<IPublicRepository>();
        publicRepository
            .Setup(item => item.SubmitAsync(It.IsAny<CurrentUser>(), It.IsAny<PublicSubmissionRequest>(), 2))
            .Callback<CurrentUser, PublicSubmissionRequest, int>((_, request, _) => capturedRequest = request)
            .ReturnsAsync(new PublicSubmissionResponse
            {
                SolutionId = 123,
                LanguageId = 2,
                CreatedAtUtc = DateTime.UtcNow
            });

        PublicService service = new PublicService(
            publicRepository.Object,
            Mock.Of<IAcademicRepository>(),
            Mock.Of<IAcademicService>(),
            Mock.Of<ISolutionService>(),
            Mock.Of<IPasswordRecoveryEmailService>(),
            Mock.Of<IWelcomeEmailService>(),
            Mock.Of<IConfiguration>());

        PublicSubmissionResponse response = await service.SubmitAsync(new CurrentUser
        {
            UserId = "student1",
            SiteId = 1,
            Role = UserRolesEnum.Invitado
        }, new PublicSubmissionRequest
        {
            ContestId = 3040,
            Num = 0,
            SourceCode = "print(42)",
            LanguageId = 2
        });

        Assert.Equal(123, response.SolutionId);
        Assert.NotNull(capturedRequest);
        Assert.Equal(3040, capturedRequest!.ContestId);
        Assert.Equal(0, capturedRequest.Num);
        Assert.Null(capturedRequest.ContestProblemId);
        Assert.Null(capturedRequest.ProblemId);
    }
}
