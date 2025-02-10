namespace ScheduleManager.Core.Domain.Models;

public partial class Compileinfo
{
    public int SolutionId { get; set; }

    public string? Error { get; set; }

    public virtual Solution Solution { get; set; } = null!;
}
