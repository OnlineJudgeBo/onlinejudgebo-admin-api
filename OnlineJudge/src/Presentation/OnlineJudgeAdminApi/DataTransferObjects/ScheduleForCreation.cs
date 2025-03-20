using System.ComponentModel.DataAnnotations;

namespace OnlineJudgeAdminApi.DataTransferObjects;

public partial class ScheduleForCreation
{
    [Required]
    public List<String> ScheduleDays { get; set; }
    public string ScheduleTime { get; set; }
    public int Subject { get; set; }
    public int Teacher { get; set; }
}
