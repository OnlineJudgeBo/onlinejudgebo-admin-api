namespace OnlineJudgeAdminApi.DataTransferObjects;

public class ProblemForUpdate
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Input { get; set; } = string.Empty;

    public string Output { get; set; } = string.Empty;

    public string SampleInput { get; set; } = string.Empty;

    public string SampleOutput { get; set; } = string.Empty;

    public virtual ICollection<ProblemSampleCaseForCreation>? SampleCases { get; set; }

    public string Hint { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string? OriginSource { get; set; } = string.Empty;

    public int TimeLimit { get; set; }

    public int MemoryLimit { get; set; }

    public string? Defunct { get; set; }

    public string Spj { get; set; } = "N";

    public virtual ICollection<ClassificationsForCreation>? Classifications { get; set; }
}
