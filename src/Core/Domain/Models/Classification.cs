namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class Classification
{
    public int? ClassificationId { get; set; } = 0;

    public int? TopicId { get; set; } = 0;

    public string? Name { get; set; } = null!;

    public virtual Topic? Topic { get; set; } = null!;
    public virtual ICollection<Problem>? Problems { get; set; }
}
