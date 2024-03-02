using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

[Table("user_roles")]
public class DbUserRole
{
    [Key, Column(Order = 0)]
    [MaxLength(48)]
    public required string UserId { get; set; }

    [Key, Column(Order = 1)]
    public int RoleId { get; set; }

    [ForeignKey("UserId")]
    public virtual required DbUser User { get; set; }

    [ForeignKey("RoleId")]
    public virtual required DbRole Role { get; set; }
}