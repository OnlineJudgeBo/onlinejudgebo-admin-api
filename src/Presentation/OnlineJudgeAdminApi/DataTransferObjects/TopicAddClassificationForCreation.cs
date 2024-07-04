namespace OnlineJudgeAdminApi.DataTransferObjects;

public partial class TopicAddClassificationForCreation
{
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
