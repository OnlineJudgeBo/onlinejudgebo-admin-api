namespace ScheduleManager.Core.Domain.Models;

public partial class ScheduleForCreationModel
{
    public List<String> ScheduleDays { get; set; }
    public string ScheduleTime { get; set; }
    public int Subject { get; set; }
    public int Teacher { get; set; }
    public int? Assistant { get; set; }
}
