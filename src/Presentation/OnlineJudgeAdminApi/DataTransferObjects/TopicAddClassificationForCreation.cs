namespace OnlineJudgeAdminApi.DataTransferObjects;

public partial class TopicAddClassificationForCreation
{
    public int TopicId { get; set; }
    public virtual ICollection<ClassificationAddToTopic> Classifications { get; set; }
}

public partial class ClassificationForUpdate
{
    public string Name { get; set; }
}

public partial class ClassificationAddToTopic
{
    public string Name { get; set; }
}
