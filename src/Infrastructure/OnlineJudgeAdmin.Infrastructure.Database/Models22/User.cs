namespace OnlineJudgeAdmin.Infrastructure.Database.Models22;

public partial class User
{
    public string UserId { get; set; } = null!;

    public string? Password { get; set; }

    public string Ip { get; set; } = null!;

    public DateTime? Accesstime { get; set; }

    public DateTime? RegTime { get; set; }

    public bool IsDeleted { get; set; }

    public string? ResetPasswordToken { get; set; }

    public DateTime? ResetPasswordExpires { get; set; }

    public bool? IsActive { get; set; }

    public virtual ICollection<News> News { get; set; } = new List<News>();

    public virtual ICollection<Solution> Solutions { get; set; } = new List<Solution>();

    public virtual UserActivity? UserActivity { get; set; }

    public virtual UserProfile? UserProfile { get; set; }

    public virtual UserSetting? UserSetting { get; set; }
}
