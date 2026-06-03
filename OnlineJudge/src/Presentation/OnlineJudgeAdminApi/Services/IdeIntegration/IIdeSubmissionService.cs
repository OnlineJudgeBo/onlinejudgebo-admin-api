using OnlineJudgeAdminApi.DataTransferObjects;

namespace OnlineJudgeAdminApi.Services.IdeIntegration;

public interface IIdeSubmissionService
{
    Task<VibeSubmissionResponse> SubmitAsync(string launchToken, VibeSubmissionForCreation submission);

    Task<VibeRunResponse> RunAsync(string launchToken, VibeSubmissionForCreation submission);

    Task<VibeSubmissionStatusResponse> GetStatusAsync(string launchToken, int solutionId);
}
