namespace OnlineJudgeAdminApi.DataTransferObjects;

public partial class TopicForCreation
{
    public int? TopicId { get; set; }

    public int? CategoryId { get; set; }

    public string? Name { get; set; } = null!;
}
