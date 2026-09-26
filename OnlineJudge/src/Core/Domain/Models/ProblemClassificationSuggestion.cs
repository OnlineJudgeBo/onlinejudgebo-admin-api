namespace OnlineJudgeAdmin.Core.Domain.Models;

public class ProblemClassificationSuggestion
{
    public bool Available { get; set; }

    public string? UnavailableReason { get; set; }

    public ICollection<Classification> Classifications { get; set; } = new List<Classification>();
}
