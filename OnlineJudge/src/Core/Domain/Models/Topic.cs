namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class Topic
{
    public int TopicId { get; set; }

    public string Name { get; set; }

    public virtual ICollection<Classification> Classifications { get; set; } = new List<Classification>();
}
