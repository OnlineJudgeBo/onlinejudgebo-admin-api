namespace ScheduleManager.Core.Domain.Models;

public partial class Contest
{
    public int ContestId { get; set; }

    public string? Title { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string Defunct { get; set; } = null!;

    public string? Description { get; set; }

    public sbyte Private { get; set; }

    public int Langmask { get; set; }

    public bool Obi { get; set; }

    public virtual ICollection<ContestProblem> ContestProblems { get; set; } = new List<ContestProblem>();

    public virtual ICollection<ContestUser>? ContestUsers { get; set; } = new List<ContestUser>();

    public virtual ICollection<ProgrammingLanguage>? ProgrammingLanguages { get; set; } = new List<ProgrammingLanguage>();
}
