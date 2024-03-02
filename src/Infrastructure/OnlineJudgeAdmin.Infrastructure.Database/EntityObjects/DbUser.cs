using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

[Table("users")]
public class DbUser
{
   [Key]
    public string UserId { get; set; }
    public string Password { get; set; }
    public string IP { get; set; }
    public DateTime? AccessTime { get; set; }
    public DateTime? RegTime { get; set; }
    public bool IsDeleted { get; set; }
    public string ResetPasswordToken { get; set; }
    public DateTime? ResetPasswordExpires { get; set; }
    public virtual ICollection<DbSolution> Solutions { get; set; }
}
