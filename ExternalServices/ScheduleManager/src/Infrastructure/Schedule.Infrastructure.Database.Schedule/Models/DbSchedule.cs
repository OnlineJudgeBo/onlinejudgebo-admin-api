namespace OnlineJudgeAdmin.Infrastructure.Database.Schedule.Models;

public partial class DbSchedule
{
    public int Id { get; set; }

    public string DayOfWeek { get; set; } = null!;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string Subject { get; set; } = null!;

    public int? TeacherId { get; set; }

    public virtual DbTeacher? Teacher { get; set; }
}
