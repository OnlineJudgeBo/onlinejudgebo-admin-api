namespace OnlineJudgeAdmin.Infrastructure.Database.Models22;

public partial class Classification
{
    public int ClassificationId { get; set; }

    public int TopicId { get; set; }

    public string Name { get; set; } = null!;

    public virtual Topic Topic { get; set; } = null!;

    public virtual ICollection<Problem> Problems { get; set; } = new List<Problem>();
}
