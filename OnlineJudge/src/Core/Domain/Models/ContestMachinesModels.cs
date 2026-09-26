namespace OnlineJudgeAdmin.Core.Domain.Models;

// Control-server group an exam's lab machines join: "contest-<id>", tokens derived from a secret.
public sealed record ControlGroup(string Id, string Label, string EnrollToken, string AdminToken);

public sealed class LabLoginResult
{
    public bool Ok { get; init; }
    public string Message { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int ContestId { get; init; }
    public ControlGroup? Group { get; init; }
}
