namespace OnlineJudgeAdminApi.DataTransferObjects;

public class BlockRuleForCreation
{
    public string Type { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Note { get; set; }
}
