namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

public partial class DbSubjectAssistant
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string Name { get; set; } = null!;
    public string Schedule { get; set; } = null!;
}
