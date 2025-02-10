using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace ScheduleManager.Infrastructure.Database.Models;

[Table("online")]
public partial class DbOnline
{
    [Key]
    [Column("hash")]
    public string Hash { get; set; } = null!;

    [Column("ip")]
    public string Ip { get; set; } = null!;

    [Column("ua")]
    public string Ua { get; set; } = null!;

    [Column("refer")]
    public string? Refer { get; set; }

    [Column("lastmove")]
    public int Lastmove { get; set; }

    [Column("firsttime")]
    public int? Firsttime { get; set; }

    [Column("uri")]
    public string? Uri { get; set; }
}
