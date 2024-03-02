using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

[Table("online")]

public class DbOnline
{
    [Key]
    [MaxLength(32)]
    public required string Hash { get; set; }

    [Required]
    [MaxLength(20)]
    public string IP { get; set; } = String.Empty;

    [Required]
    [MaxLength(255)]
    public string UA { get; set; } = String.Empty;

    [MaxLength(255)]
    public string Refer { get; set; } = String.Empty;

    public int LastMove { get; set; }

    public int? FirstTime { get; set; }

    [MaxLength(255)]
    public string Uri { get; set; } = String.Empty;
}
