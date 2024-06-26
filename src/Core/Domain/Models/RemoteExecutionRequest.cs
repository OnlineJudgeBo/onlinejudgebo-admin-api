namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class RemoteExecutionRequest
{
    public int ClientSubmitId { get; set; }

    public string ClientSource { get; set; }

    public int JudgeLanguageId { get; set; }

    public int JudgeProblemId { get; set; }

    public int ClientId { get; set; }
}
