namespace OnlineJudgeAdminApi.DataTransferObjects;

public class PublicPasswordRecoveryRequestForCreation
{
    public string Email { get; set; } = string.Empty;

    public int SiteId { get; set; }
}
