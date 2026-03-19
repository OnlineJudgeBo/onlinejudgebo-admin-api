namespace OnlineJudgeAdminApi.DataTransferObjects;

public class PublicRegisterForCreation
{
    public string UserId { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Nick { get; set; }

    public string? LastName { get; set; }

    public string? School { get; set; }

    public int SiteId { get; set; }
}
