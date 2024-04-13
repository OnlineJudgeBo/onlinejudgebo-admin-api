using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("programing_language")]
public partial class DbProgrammingLanguage
{
    [Key]
    [Column("language_id")]
    public int? LanguageId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<DbContest> Contests { get; set; }
}
