using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Core.Domain.Models;
public class ContestProblem
{
    public int ContestId { get; set; }
    public int ProblemId { get; set; }
    public string Title { get; set; }
    public int Num { get; set; }
    public virtual Contest Contest { get; set; }
    public virtual Problem Problem { get; set; }
}
