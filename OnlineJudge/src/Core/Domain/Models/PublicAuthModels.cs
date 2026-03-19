namespace OnlineJudgeAdmin.Core.Domain.Models;

public class PublicAuthenticatedUser
{
    public string UserId { get; set; } = string.Empty;

    public string Nick { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = nameof(UserRolesEnum.Invitado);

    public int SiteId { get; set; }
}

public class PublicPasswordRecoveryTarget
{
    public string UserId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Nick { get; set; } = string.Empty;
}

public class PublicAuthResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public PublicAuthenticatedUser User { get; set; } = new();
}
