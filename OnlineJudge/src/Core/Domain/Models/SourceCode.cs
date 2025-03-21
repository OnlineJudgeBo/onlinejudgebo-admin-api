namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class SourceCode
{
    public int SolutionId { get; set; }

    public string Source { get; set; } = null!;

    public virtual Solution Solution { get; set; } = null!;
}
