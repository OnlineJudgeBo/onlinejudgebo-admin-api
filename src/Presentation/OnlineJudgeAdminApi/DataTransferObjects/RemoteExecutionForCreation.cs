namespace OnlineJudgeAdminApi.DataTransferObjects;

public partial class RemoteExecutionForCreation
{
    public int ClientId { get; set; }
    public int ClientSubmitId { get; set; }
    public string ClientSource { get; set; }
    public int JudgeLanguageId { get; set; }
    public int JudgeProblemId { get; set; }
}
