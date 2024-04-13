using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("contest")]
public partial class DbContest
{
    [Key]
    [Column("contest_id")]
    public int ContestId { get; set; }

    [Column("title")]
    public string? Title { get; set; }

    [Column("start_time")]
    public DateTime StartTime { get; set; }

    [Column("end_time")]
    public DateTime EndTime { get; set; }

    [Column("defunct")]
    public string Defunct { get; set; } = null!;

    [Column("description", TypeName = "text")]
    public string? Description { get; set; }

    [Column("private")]
    public sbyte Private { get; set; }

    [Column("langmask")]
    public int Langmask { get; set; }

    [Column("obi")]
    public bool Obi { get; set; }

    public virtual ICollection<DbContestProblem>? ContestProblems { get; set; } = new List<DbContestProblem>();
    public ICollection<DbContestUser>? ContestUsers { get; set; } = new List<DbContestUser>();
    public virtual ICollection<DbProgrammingLanguage>? ProgrammingLanguages { get; set; } = new List<DbProgrammingLanguage>();
}
