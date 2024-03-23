namespace OnlineJudgeAdmin.Infrastructure.Database.Models22;

public partial class ContestProblem
{
    public int ContestId { get; set; }

    public int ProblemId { get; set; }

    public string Title { get; set; } = null!;

    public int Num { get; set; }

    public virtual Contest Contest { get; set; } = null!;

    public virtual Problem Problem { get; set; } = null!;
}
