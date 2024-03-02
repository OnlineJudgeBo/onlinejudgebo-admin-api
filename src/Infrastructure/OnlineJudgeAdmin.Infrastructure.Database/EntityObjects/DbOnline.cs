using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

[Table("online")]
public class DbOnline
{
    [Key]
    [Column("hash")]
    [MaxLength(32)]
    public string Hash { get; set; }

    [Column("ip")]
    [Required]
    [MaxLength(20)]
    public string IP { get; set; } = string.Empty;

    [Column("ua")]
    [Required]
    [MaxLength(255)]
    public string UA { get; set; } = string.Empty;

    [Column("refer")]
    [MaxLength(255)]
    public string Refer { get; set; } = string.Empty;

    [Column("last_move")]
    public int LastMove { get; set; }

    [Column("first_time")]
    public int? FirstTime { get; set; }

    [Column("uri")]
    [MaxLength(255)]
    public string Uri { get; set; } = string.Empty;
}
