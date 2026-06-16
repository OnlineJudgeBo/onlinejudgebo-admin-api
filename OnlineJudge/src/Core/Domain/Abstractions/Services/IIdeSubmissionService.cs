using OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IIdeSubmissionService
{
    Task<IdeSubmissionResponse> SubmitAsync(string launchToken, IdeSubmissionRequest submission);

    Task<IdeRunResponse> RunAsync(string launchToken, IdeSubmissionRequest submission);

    Task<IdeRunResponse> CustomInputAsync(string launchToken, IdeSubmissionRequest submission);

    Task<IdeSubmissionStatusResponse> GetStatusAsync(string launchToken, int solutionId);
}
