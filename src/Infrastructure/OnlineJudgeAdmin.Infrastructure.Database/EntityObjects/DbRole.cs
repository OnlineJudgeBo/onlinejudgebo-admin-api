using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

[Table("roles")]
public class DbRole
{
    [Key]
    [Column("role_id")]
    public int RoleId { get; set; }

    [Column("role_name")]
    [Required]
    [MaxLength(50)]
    public string RoleName { get; set; }
}
