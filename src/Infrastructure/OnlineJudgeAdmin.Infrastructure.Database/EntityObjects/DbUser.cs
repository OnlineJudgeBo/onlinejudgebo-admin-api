using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

[Table("users")]
public class DbUser
{
    [Key]
    [Column("user_id")]
    public string UserId { get; set; }

    [Column("password")]
    public string Password { get; set; }

    [Column("ip")]
    public string IP { get; set; }

    [Column("access_time")]
    public DateTime? AccessTime { get; set; }

    [Column("reg_time")]
    public DateTime? RegTime { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("reset_password_token")]
    public string ResetPasswordToken { get; set; }

    [Column("reset_password_expires")]
    public DateTime? ResetPasswordExpires { get; set; }

    public virtual ICollection<DbSolution> Solutions { get; set; }

    public DbUser()
    {
        Solutions = new HashSet<DbSolution>();
    }
}
