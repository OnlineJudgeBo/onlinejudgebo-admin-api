namespace OnlineJudgeAdmin.Infrastructure.Database.Models22;

public partial class SourceCode
{
    public int SolutionId { get; set; }

    public string Source { get; set; } = null!;

    public virtual Solution Solution { get; set; } = null!;
}
