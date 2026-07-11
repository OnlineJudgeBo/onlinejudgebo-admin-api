namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class Subject
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Schedule> Schedules { get; set; } = new HashSet<Schedule>();
}
