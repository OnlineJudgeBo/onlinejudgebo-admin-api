namespace OnlineJudgeAdmin.Core.Domain.Models;

public class ProblemSample
{
    public int ProblemId { get; set; }

    public int Num { get; set; }

    public string? Input { get; set; }

    public string? Output { get; set; }
}
