namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class Schedule
{
    public int ScheduleId { get; set; }

    public string DayOfWeek { get; set; } = null!;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string Subject { get; set; } = null!;

    public int? TeacherId { get; set; }

    public virtual Teacher? Teacher { get; set; }
}
