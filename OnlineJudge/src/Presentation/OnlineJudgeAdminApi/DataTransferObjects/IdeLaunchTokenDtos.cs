namespace OnlineJudgeAdminApi.DataTransferObjects;

public class IdeLaunchTokenForCreation
{
    public int ProblemId { get; set; }

    public int? ContestId { get; set; }

    public int? Num { get; set; }

    public int[]? AllowedLanguages { get; set; }
}

public class IdeLaunchTokenResponse
{
    public string Token { get; set; } = string.Empty;
}
