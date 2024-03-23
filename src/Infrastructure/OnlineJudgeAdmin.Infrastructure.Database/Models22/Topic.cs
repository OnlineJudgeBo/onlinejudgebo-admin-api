namespace OnlineJudgeAdmin.Infrastructure.Database.Models22;

public partial class Topic
{
    public int TopicId { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Classification> Classifications { get; set; } = new List<Classification>();
}
