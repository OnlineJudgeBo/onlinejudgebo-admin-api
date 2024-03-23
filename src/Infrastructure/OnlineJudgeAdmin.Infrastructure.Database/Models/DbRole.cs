using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("roles")]
public partial class DbRole
{
    [Key]
    [Column("role_id")]
    public int RoleId { get; set; }

    [Column("role_name")]
    public string RoleName { get; set; } = null!;

    public virtual ICollection<DbUserRole> UserRole { get; set; }
}
