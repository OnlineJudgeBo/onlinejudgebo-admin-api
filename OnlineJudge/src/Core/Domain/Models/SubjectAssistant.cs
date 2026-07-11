namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class SubjectAssistant
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty;
}
