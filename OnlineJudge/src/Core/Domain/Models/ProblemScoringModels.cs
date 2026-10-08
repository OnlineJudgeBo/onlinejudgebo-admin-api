namespace OnlineJudgeAdmin.Core.Domain.Models;

// Test groups (subtasks) of a problem, stored as scoring.json next to its test data and read by the judge kernel.
public sealed class ProblemScoreGroup
{
    public string Name { get; set; } = string.Empty;

    public decimal Points { get; set; }

    // sum: proportional to the tests passed. min: all or nothing. mul: product of the outcomes.
    public string Type { get; set; } = "sum";

    // Globs over the test name without ".in"; a test may belong to several groups.
    public List<string> Tests { get; set; } = [];
}

public sealed class ProblemScoring
{
    public List<ProblemScoreGroup> Groups { get; set; } = [];

    // Read only: the test names the globs are matched against.
    public IReadOnlyList<string> AvailableTests { get; set; } = Array.Empty<string>();
}
