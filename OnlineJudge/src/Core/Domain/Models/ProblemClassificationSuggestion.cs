namespace OnlineJudgeAdmin.Core.Domain.Models;

public class ProblemClassificationSuggestion
{
    public bool Available { get; set; }

    public string? UnavailableReason { get; set; }

    public ICollection<Classification> Classifications { get; set; } = new List<Classification>();

    // classificationId -> short reason the model gave for choosing it (shown next to the checkbox).
    public IDictionary<int, string> Reasons { get; set; } = new Dictionary<int, string>();
}
