using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("user_roles")]
public partial class DbUserRole
{
    [Key, Column(Order = 0)]
    [ForeignKey("User")]
    public string UserId { get; set; }

    [Key, Column(Order = 1)]
    [ForeignKey("Role")]
    public int RoleId { get; set; }

    public virtual DbRole Role { get; set; } = null!;
    public virtual DbUser User { get; set; } = null!;
}
