namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class Classification
{
    public int? ClassificationId { get; set; }

    public int? TopicId { get; set; }

    public string? Name { get; set; }

    public virtual Topic? Topic { get; set; }
    public virtual ICollection<Problem>? Problems { get; set; }
}
