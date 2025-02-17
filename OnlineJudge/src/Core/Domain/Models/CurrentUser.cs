namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class CurrentUser
{
    public string UserId { get; set; }

    public UserRolesEnum Role { get; set; }
    public int SiteId { get; set; }
}
