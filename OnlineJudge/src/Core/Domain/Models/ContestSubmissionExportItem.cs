namespace OnlineJudgeAdmin.Core.Domain.Models;

// One submission of a contest with its code, to review it outside the judge.
public sealed record ContestSubmissionExportItem(
    int SolutionId,
    string UserId,
    string Nick,
    int ProblemNum,
    int ProblemId,
    string LanguageName,
    short Result,
    decimal PassRate,
    int TimeMs,
    int MemoryKb,
    string Ip,
    DateTime InDate,
    string Source);
