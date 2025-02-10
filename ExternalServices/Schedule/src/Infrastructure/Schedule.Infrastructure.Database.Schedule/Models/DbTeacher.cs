namespace OnlineJudgeAdmin.Infrastructure.Database.Schedule.Models;

public partial class DbTeacher
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<DbSchedule> Schedules { get; set; } = new List<DbSchedule>();
}
