namespace OnlineJudgeAdmin.Infrastructure.Database.Models22;

public partial class Problem
{
    public int ProblemId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string? Input { get; set; }

    public string? Output { get; set; }

    public string? SampleInput { get; set; }

    public string? SampleOutput { get; set; }

    public string Spj { get; set; } = null!;

    public string? Hint { get; set; }

    public string? Source { get; set; }

    public DateTime? InDate { get; set; }

    public int TimeLimit { get; set; }

    public int MemoryLimit { get; set; }

    public string Defunct { get; set; } = null!;

    public int? Accepted { get; set; }

    public int? Submit { get; set; }

    public int? Solved { get; set; }

    public virtual ICollection<ContestProblem> ContestProblems { get; set; } = new List<ContestProblem>();

    public virtual ICollection<Solution> Solutions { get; set; } = new List<Solution>();

    public virtual ICollection<Classification> Classifications { get; set; } = new List<Classification>();
}
