namespace OnlineJudgeAdminApi.DataTransferObjects;

public class PatitoIdeSubmissionStatusResponse
{
    public string SubmissionId { get; set; } = string.Empty;

    public string Id { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string Verdict { get; set; } = string.Empty;

    public string Stdout { get; set; } = string.Empty;

    public string Stderr { get; set; } = string.Empty;

    public string CompileErrors { get; set; } = string.Empty;

    public IReadOnlyCollection<string> Logs { get; set; } = Array.Empty<string>();

    public int RuntimeMs { get; set; }

    public int MemoryKb { get; set; }
}
