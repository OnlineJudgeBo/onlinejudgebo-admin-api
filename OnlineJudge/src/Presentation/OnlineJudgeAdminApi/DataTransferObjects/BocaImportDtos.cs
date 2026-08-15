namespace OnlineJudgeAdminApi.DataTransferObjects;

public class BocaImportPreviewResult
{
    public string? StagingId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public bool Success { get; set; }

    public string? Error { get; set; }

    public string? Title { get; set; }

    public int? TimeLimit { get; set; }

    public int? MemoryLimit { get; set; }

    public bool NeedsReview { get; set; }

    public List<string> ReviewReasons { get; set; } = [];

    public int TestCaseCount { get; set; }

    public string? DescriptionPreview { get; set; }

    public string? SampleInputPreview { get; set; }

    public string? SampleOutputPreview { get; set; }
}

public class BocaImportConfirmRequest
{
    public List<string> StagingIds { get; set; } = [];
}

public class BocaImportConfirmResult
{
    public string StagingId { get; set; } = string.Empty;

    public bool Success { get; set; }

    public int? ProblemId { get; set; }

    public string? Error { get; set; }
}
