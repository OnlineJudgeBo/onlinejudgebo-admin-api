using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ScheduleManager.Infrastructure.Database.Models;

public partial class DbSchedule
{
    public int Id { get; set; }

    public string DayOfWeek { get; set; } = null!;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public int SubjectId { get; set; }

    [ForeignKey("SubjectId")]
    public virtual DbSubject Subject { get; set; } = null!;

    public int? TeacherId { get; set; }

    [ForeignKey("TeacherId")]
    [JsonIgnore]
    public virtual DbTeacher? Teacher { get; set; }

    public int? AssistantId { get; set; }

    [ForeignKey("AssistantId")]
    [JsonIgnore]
    public virtual DbTeacher? Assistant { get; set; }
}
