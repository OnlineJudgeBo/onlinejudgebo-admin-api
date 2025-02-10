using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace ScheduleManager.Infrastructure.Database.Models;

[Table("user_activity")]
public partial class DbUserActivity
{
    [Key]
    [Column("user_id")]
    public string UserId { get; set; } = null!;

    [Column("site_id")]
    public int SiteId { get; set; }

    [Column("submit")]
    public int? Submit { get; set; }

    [Column("solved")]
    public int? Solved { get; set; }

    public virtual DbUser User { get; set; } = null!;
}
