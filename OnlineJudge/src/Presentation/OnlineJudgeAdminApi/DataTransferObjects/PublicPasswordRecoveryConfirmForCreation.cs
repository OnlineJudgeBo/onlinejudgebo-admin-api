namespace OnlineJudgeAdminApi.DataTransferObjects;

public class PublicPasswordRecoveryConfirmForCreation
{
    public string Email { get; set; } = string.Empty;

    public string RecoveryCode { get; set; } = string.Empty;

    public int SiteId { get; set; }
}
