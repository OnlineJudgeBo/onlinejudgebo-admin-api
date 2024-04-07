namespace OnlineJudgeAdminApi.DataTransferObjects;

public class ProblemForUpdate
{
    public string Title { get; set; }

    public string Description { get; set; }

    public string Input { get; set; }

    public string Output { get; set; }

    public string SampleInput { get; set; }

    public string SampleOutput { get; set; }

    public string Hint { get; set; }

    public string Source { get; set; }

    public int TimeLimit { get; set; }

    public int MemoryLimit { get; set; }

    public string Defunct { get; set; } = "Y";

    public string Spj { get; set; } = "N";

    public virtual ICollection<ClassificationsForCreation>? Classifications { get; set; }
}
