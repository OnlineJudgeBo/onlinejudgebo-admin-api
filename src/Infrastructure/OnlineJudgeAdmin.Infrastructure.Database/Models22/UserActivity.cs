namespace OnlineJudgeAdmin.Infrastructure.Database.Models22;

public partial class UserActivity
{
    public string UserId { get; set; } = null!;

    public int? Submit { get; set; }

    public int? Solved { get; set; }

    public virtual User User { get; set; } = null!;
}
