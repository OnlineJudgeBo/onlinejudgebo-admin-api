namespace OnlineJudgeAdmin.Infrastructure.Database.Models22;

public partial class Compileinfo
{
    public int SolutionId { get; set; }

    public string? Error { get; set; }

    public virtual Solution Solution { get; set; } = null!;
}
