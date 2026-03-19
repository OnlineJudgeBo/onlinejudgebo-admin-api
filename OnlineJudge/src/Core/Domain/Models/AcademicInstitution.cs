namespace OnlineJudgeAdmin.Core.Domain.Models;

public class AcademicInstitution
{
    public long InstitutionId { get; set; }

    public int SiteId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ShortName { get; set; }

    public bool IsActive { get; set; }
}
