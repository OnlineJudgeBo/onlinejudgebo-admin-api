using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("privilege")]
public partial class DbPrivilege
{
    [Key]
    [Column("privilege_id")]
    public int PrivilegeId { get; set; }

    [Column("user_id")]
    public string UserId { get; set; } = null!;

    [Column("rightstr")]
    public string Rightstr { get; set; } = null!;

    [Column("defunct")]
    public string Defunct { get; set; } = null!;
}
