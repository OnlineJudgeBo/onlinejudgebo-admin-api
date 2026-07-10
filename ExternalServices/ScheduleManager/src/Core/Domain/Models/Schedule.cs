namespace ScheduleManager.Core.Domain.Models;

public partial class Schedule
{
    public int Id { get; set; }

    public string DayOfWeek { get; set; } = null!;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public Subject Subject { get; set; } = null!;

    public Teacher? Teacher { get; set; }
    public Teacher? Assistant { get; set; }
}
