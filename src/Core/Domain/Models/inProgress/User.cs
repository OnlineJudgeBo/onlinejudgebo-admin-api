using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Core.Domain.Models;

public class User
{
    public string UserId { get; set; }
    public string Password { get; set; }
    public string IP { get; set; }
    public DateTime? AccessTime { get; set; }
    public DateTime? RegTime { get; set; }
    public bool IsDeleted { get; set; }
    public string ResetPasswordToken { get; set; }
    public DateTime? ResetPasswordExpires { get; set; }
    public virtual ICollection<Solution> Solutions { get; set; }
}
