namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class ProgrammingLanguage
{
    public int? LanguageId { get; set; }

    public string? Name { get; set; }

    public ICollection<Contest> Contests { get; set; }
}
