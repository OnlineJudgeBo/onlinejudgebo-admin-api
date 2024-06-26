using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("users")]
public partial class DbUser
{
    [Key]
    [Column("user_id")]
    public string UserId { get; set; } = null!;

    [Column("password")]
    public string? Password { get; set; }

    [Column("ip")]
    public string Ip { get; set; } = null!;

    [Column("accesstime")]
    public DateTime? Accesstime { get; set; }

    [Column("reg_time")]
    public DateTime? RegTime { get; set; }

    [Column("reset_password_token")]
    public string? ResetPasswordToken { get; set; }

    [Column("reset_password_expires")]
    public DateTime? ResetPasswordExpires { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    public virtual ICollection<DbNews> News { get; set; }

    public virtual ICollection<DbSolution> Solutions { get; set; }

    public virtual DbUserActivity? UserActivity { get; set; }

    public virtual DbUserProfile? UserProfile { get; set; }

    public virtual DbUserSetting? UserSetting { get; set; }

    public virtual ICollection<DbRole> Roles { get; set; } = new List<DbRole>();

    public ICollection<DbContestUser>? ContestUsers { get; set; }

    public ICollection<DbUserRole>? UserRoles { get; set; } = new List<DbUserRole>();
}
