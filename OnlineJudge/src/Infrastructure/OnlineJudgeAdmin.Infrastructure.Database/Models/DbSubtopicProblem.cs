using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("subtopic_problem")]
public class DbSubtopicProblem
{
    [Column("subtopic_id")]
    public long SubtopicId { get; set; }

    [Column("problem_id")]
    public int ProblemId { get; set; }

    [Column("role_in_topic")]
    public string RoleInTopic { get; set; } = "core";

    [Column("sort_order")]
    public int SortOrder { get; set; }
}
