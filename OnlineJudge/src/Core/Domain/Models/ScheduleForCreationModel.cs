namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class ScheduleForCreationModel
{
    public List<string> ScheduleDays { get; set; } = new();
    public string ScheduleTime { get; set; } = string.Empty;
    public int Subject { get; set; }
    public int Teacher { get; set; }
}
