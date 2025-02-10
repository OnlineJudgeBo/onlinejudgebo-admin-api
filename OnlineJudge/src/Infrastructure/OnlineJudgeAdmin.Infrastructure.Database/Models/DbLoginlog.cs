using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace ScheduleManager.Infrastructure.Database.Models;

[Table("loginlog")]
public partial class DbLoginlog
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public string UserId { get; set; } = null!;

    [Column("password")]
    public string? Password { get; set; }

    [Column("ip")]
    public string? Ip { get; set; }

    [Column("time")]
    public DateTime? Time { get; set; }
}
