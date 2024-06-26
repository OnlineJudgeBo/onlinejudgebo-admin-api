namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class UserRole
{
    public string UserId { get; set; } = null!;

    public int RoleId { get; set; }

    public virtual Role Role { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
