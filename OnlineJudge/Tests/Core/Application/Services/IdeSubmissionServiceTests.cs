using Moq;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Services.IdeIntegration;

public class IdeSubmissionServiceTests
{
    [Fact]
    public async Task SubmitAsync_PreservesContestClaimsForContestProblemA()
    {
        var validator = new Mock<IIdeLaunchTokenValidator>();
        validator
            .Setup(item => item.Validate("launch-token"))
            .Returns(new IdeLaunchClaims(
                "student1",
                SiteId: 1,
                ProblemId: 1000,
                ContestId: 3040,
                Num: 0,
                AllowedLanguages: new[] { 2 }));

        PublicSubmissionRequest? capturedRequest = null;
        CurrentUser? capturedUser = null;
        var publicService = new Mock<IPublicService>();
        publicService
            .Setup(item => item.SubmitAsync(It.IsAny<CurrentUser>(), It.IsAny<PublicSubmissionRequest>()))
            .Callback<CurrentUser, PublicSubmissionRequest>((user, request) =>
            {
                capturedUser = user;
                capturedRequest = request;
            })
            .ReturnsAsync(new PublicSubmissionResponse
            {
                SolutionId = 123,
                LanguageId = 2,
                CreatedAtUtc = DateTime.UtcNow
            });

        var service = new IdeSubmissionService(validator.Object, publicService.Object);

        var response = await service.SubmitAsync("launch-token", new VibeSubmissionForCreation
        {
            ProblemId = "1000",
            SourceCode = "print(42)",
            LanguageId = 2
        });

        Assert.Equal("123", response.SubmissionId);
        Assert.NotNull(capturedUser);
        Assert.Equal("student1", capturedUser!.UserId);
        Assert.Equal(1, capturedUser.SiteId);
        Assert.NotNull(capturedRequest);
        Assert.Equal(1000, capturedRequest!.ProblemId);
        Assert.Equal(3040, capturedRequest.ContestId);
        Assert.Equal("A", capturedRequest.ContestProblemId);
        Assert.Equal(2, capturedRequest.LanguageId);
    }

    [Fact]
    public async Task SubmitAsync_UsesPayloadContestMetadataWhenTokenDoesNotCarryContestClaims()
    {
        var validator = new Mock<IIdeLaunchTokenValidator>();
        validator
            .Setup(item => item.Validate("launch-token"))
            .Returns(new IdeLaunchClaims(
                "student1",
                SiteId: 1,
                ProblemId: 1000,
                ContestId: null,
                Num: null,
                AllowedLanguages: new[] { 2 }));

        PublicSubmissionRequest? capturedRequest = null;
        var publicService = new Mock<IPublicService>();
        publicService
            .Setup(item => item.SubmitAsync(It.IsAny<CurrentUser>(), It.IsAny<PublicSubmissionRequest>()))
            .Callback<CurrentUser, PublicSubmissionRequest>((_, request) => capturedRequest = request)
            .ReturnsAsync(new PublicSubmissionResponse
            {
                SolutionId = 456,
                LanguageId = 2,
                CreatedAtUtc = DateTime.UtcNow
            });

        var service = new IdeSubmissionService(validator.Object, publicService.Object);

        var response = await service.SubmitAsync("launch-token", new VibeSubmissionForCreation
        {
            ProblemId = "1000",
            ContestId = 3040,
            Num = 0,
            SourceCode = "print(42)",
            LanguageId = 2
        });

        Assert.Equal("456", response.SubmissionId);
        Assert.NotNull(capturedRequest);
        Assert.Equal(3040, capturedRequest!.ContestId);
        Assert.Equal("A", capturedRequest.ContestProblemId);
    }

    [Fact]
    public async Task SubmitAsync_RejectsContestMismatchBetweenTokenAndPayload()
    {
        var validator = new Mock<IIdeLaunchTokenValidator>();
        validator
            .Setup(item => item.Validate("launch-token"))
            .Returns(new IdeLaunchClaims(
                "student1",
                SiteId: 1,
                ProblemId: 1000,
                ContestId: 3040,
                Num: 0,
                AllowedLanguages: new[] { 2 }));

        var publicService = new Mock<IPublicService>(MockBehavior.Strict);
        var service = new IdeSubmissionService(validator.Object, publicService.Object);

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SubmitAsync("launch-token", new VibeSubmissionForCreation
        {
            ProblemId = "1000",
            ContestId = 9999,
            Num = 0,
            SourceCode = "print(42)",
            LanguageId = 2
        }));

        Assert.Equal("El token de IDE no permite enviar a este concurso.", error.Message);
    }
}
