namespace OnlineJudgeAdmin.Core.Domain.Models;

public sealed record ProblemIssue(string Code, string Message);

// What the review of a problem found. The repository fills the facts kept in the database;
// the tests and the issues come from the files of the problem.
public sealed class ProblemReview
{
    public int ProblemId { get; set; }

    public string Title { get; set; } = string.Empty;

    public List<string> Sites { get; set; } = [];

    public bool Hidden { get; set; }

    public bool HasDescription { get; set; }

    public bool HasInput { get; set; }

    public bool HasOutput { get; set; }

    public int Samples { get; set; }

    public bool SpecialJudge { get; set; }

    public int Accepted { get; set; }

    public int Tests { get; set; }

    public List<ProblemIssue> Issues { get; set; } = [];
}
