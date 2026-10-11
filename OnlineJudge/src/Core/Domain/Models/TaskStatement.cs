namespace OnlineJudgeAdmin.Core.Domain.Models;

// Statement sections, read from the task's LaTeX source or transcribed from its PDF.
public sealed record TaskStatement(string Description, string Input, string Output, string Hint);
