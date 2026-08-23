using System.Text.Json.Serialization;

namespace OnlineJudgeAdminApi.DataTransferObjects;

public class ProblemForCreation
{
    public int ProblemId { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = String.Empty;

    public string Input { get; set; } = String.Empty;

    public string Output { get; set; } = String.Empty;

    public string SampleInput { get; set; } = String.Empty;

    public string SampleOutput { get; set; } = String.Empty;

    public virtual ICollection<ProblemSampleCaseForCreation>? SampleCases { get; set; }

    public string Spj { get; set; } = "N";

    public string Hint { get; set; } = String.Empty;

    public string Source { get; set; } = String.Empty;

    public string OriginSource { get; set; } = String.Empty;

    public DateTime InDate { get; set; } = DateTime.Now;

    public int TimeLimit { get; set; } = 0;

    public int MemoryLimit { get; set; } = 128;

    public string Defunct { get; set; } = "N";

    [JsonIgnore]
    public int Accepted { get; set; } = 0;

    [JsonIgnore]
    public int Submit { get; set; } = 0;

    [JsonIgnore]
    public int Solved { get; set; } = 0;

    public virtual ICollection<ClassificationsForCreation>? Classifications { get; set; }
}
