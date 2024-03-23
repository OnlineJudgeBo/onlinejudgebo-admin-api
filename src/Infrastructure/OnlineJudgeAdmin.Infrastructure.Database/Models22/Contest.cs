namespace OnlineJudgeAdmin.Infrastructure.Database.Models22;

public partial class Contest
{
    public int ContestId { get; set; }

    public string? Title { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public string Defunct { get; set; } = null!;

    public string? Description { get; set; }

    public sbyte Private { get; set; }

    /// <summary>
    /// bits for LANG to mask
    /// </summary>
    public int Langmask { get; set; }

    public bool Obi { get; set; }

    public virtual ICollection<ContestProblem> ContestProblems { get; set; } = new List<ContestProblem>();
}
