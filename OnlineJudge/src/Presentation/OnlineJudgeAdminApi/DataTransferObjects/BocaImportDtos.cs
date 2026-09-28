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

    // Suggestions only -- nothing here is attached to the problem until the admin
    // includes the chosen ids in /confirm's SelectedClassificationIdsByStagingId.
    public List<BocaClassificationSuggestionDto> SuggestedClassifications { get; set; } = [];
}

public class BocaClassificationSuggestionDto
{
    public int ClassificationId { get; set; }

    // "<Topic> > <Classification>", e.g. "Grafos > DFS/BFS" -- matches how
    // ProblemClassifierService lists options to the model, so what the admin sees here
    // is the same label the model chose from.
    public string Label { get; set; } = string.Empty;

    // Why the model chose it, one short sentence.
    public string Reason { get; set; } = string.Empty;
}

public class BocaImportConfirmRequest
{
    public List<string> StagingIds { get; set; } = [];

    // Optional; keyed by StagingId. A staging id absent from this dictionary (or the
    // whole field left null) simply gets no classifications -- the suggestion in
    // /preview is never applied on its own.
    public Dictionary<string, List<int>>? SelectedClassificationIdsByStagingId { get; set; }
}

public class BocaImportConfirmResult
{
    public string StagingId { get; set; } = string.Empty;

    public bool Success { get; set; }

    public int? ProblemId { get; set; }

    public string? Error { get; set; }
}
