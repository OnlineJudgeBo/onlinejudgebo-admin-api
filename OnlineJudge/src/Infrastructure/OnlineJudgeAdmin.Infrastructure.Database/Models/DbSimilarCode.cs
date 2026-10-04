using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

// Written by the judge kernel (anti_cheating): best Dolos match of an accepted contest solution.
[Table("similar_code")]
public class DbSimilarCode
{
    [Column("solution_id")]
    public int SolutionId { get; set; }

    [Column("similar_s_id")]
    public int SimilarSId { get; set; }

    [Column("percentage")]
    public double Percentage { get; set; }
}
