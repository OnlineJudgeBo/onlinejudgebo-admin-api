using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("user_roles")]

public partial class DbUserRole
{
    [Key]
    [Column("user_id")]
    public string UserId { get; set; } = null!;

    [Column("role_id")]
    public int RoleId { get; set; }

    public virtual DbRole Role { get; set; } = null!;
    public virtual DbUser User { get; set; } = null!;
}
