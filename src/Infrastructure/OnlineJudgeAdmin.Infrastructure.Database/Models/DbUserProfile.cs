using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("user_profiles")]
public partial class DbUserProfile
{
    [Key]
    [Column("user_id")]
    public string UserId { get; set; } = null!;

    [Column("email")]
    public string? Email { get; set; }

    [Column("nick")]
    public string Nick { get; set; } = null!;

    [Column("school")]
    public string? School { get; set; }

    [Column("lastname")]
    public string? Lastname { get; set; }

    [Column("pais_id")]
    public int? PaisId { get; set; }

    [Column("ci", TypeName = "text")]
    public string? Ci { get; set; }

    [Column("departament")]
    public int Departament { get; set; }

    [Column("district", TypeName = "text")]
    public string? District { get; set; }

    public virtual DbUser User { get; set; } = null!;
}
