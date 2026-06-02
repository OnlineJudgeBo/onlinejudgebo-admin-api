namespace OnlineJudgeAdminApi.DataTransferObjects;

public class PublicLoginForCreation
{
    public string UserId { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public int SiteId { get; set; }
}
