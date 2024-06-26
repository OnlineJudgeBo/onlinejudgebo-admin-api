using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("user_settings")]
public partial class DbUserSetting
{
    [Key]
    [Column("user_id")]
    public string UserId { get; set; } = null!;

    [Column("volume")]
    public int Volume { get; set; }

    [Column("language")]
    public int Language { get; set; }

    [Column("obi")]
    public int? Obi { get; set; }

    [Column("institucion_id")]
    public int InstitucionId { get; set; }

    [Column("vcyt", TypeName = "text")]
    public string? Vcyt { get; set; }

    [Column("rude", TypeName = "text")]
    public string? Rude { get; set; }

    [Column("level")]
    public int? Level { get; set; }

    public virtual DbUser User { get; set; } = null!;
}
