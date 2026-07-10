namespace ScheduleManager.Infrastructure.Database.Models;

public class DbAssistant
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string Name { get; set; } = null!;
    public string Schedule { get; set; } = null!;

    public virtual DbSubject Subject { get; set; } = null!;
}
