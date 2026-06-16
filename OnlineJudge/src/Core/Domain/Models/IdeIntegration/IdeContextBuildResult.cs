namespace OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;

public enum IdeContextBuildStatus
{
    Success,
    UserNotAllowed,
    ProblemNotFound,
}

public sealed record IdeContextBuildResult(IdeContextBuildStatus Status, IdeContextResponse? Response)
{
    public static IdeContextBuildResult Success(IdeContextResponse response)
    {
        return new IdeContextBuildResult(IdeContextBuildStatus.Success, response);
    }

    public static IdeContextBuildResult UserNotAllowed()
    {
        return new IdeContextBuildResult(IdeContextBuildStatus.UserNotAllowed, null);
    }

    public static IdeContextBuildResult ProblemNotFound()
    {
        return new IdeContextBuildResult(IdeContextBuildStatus.ProblemNotFound, null);
    }
}
