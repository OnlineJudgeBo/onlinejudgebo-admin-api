namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class Roles
{
    public string UserId { get; set; }

    public List<string> RoleList { get; set; } = new List<string>();
}
