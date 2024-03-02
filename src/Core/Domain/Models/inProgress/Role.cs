using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Core.Domain.Models;

public class Role
{
    public int RoleId { get; set; }

    public string RoleName { get; set; }
}
