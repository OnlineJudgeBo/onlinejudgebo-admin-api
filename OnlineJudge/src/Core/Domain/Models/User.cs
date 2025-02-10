namespace ScheduleManager.Core.Domain.Models;

public partial class User
{
    public string UserId { get; set; } = null!;

    public string? Password { get; set; }

    public string? Ip { get; set; } = null!;

    public DateTime? Accesstime { get; set; }

    public DateTime? RegTime { get; set; }

    public string? ResetPasswordToken { get; set; }

    public DateTime? ResetPasswordExpires { get; set; }

    public bool? isActive { get; set; }

    public bool? isDeleted { get; set; }

    public virtual ICollection<News>? News { get; set; }

    public virtual ICollection<Solution>? Solutions { get; set; }

    public virtual UserActivity? UserActivity { get; set; }

    public virtual UserProfile? UserProfile { get; set; }

    public virtual UserSetting? UserSetting { get; set; }

    public virtual ICollection<Role>? Roles { get; set; }

    public ICollection<ContestUser>? ContestUsers { get; set; }

    public ICollection<UserRole>? UserRoles { get; set; } = new List<UserRole>();
}
