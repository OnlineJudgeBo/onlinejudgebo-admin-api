using System.ComponentModel.DataAnnotations;

namespace OnlineJudgeAdminApi.DataTransferObjects;

public class SubjectForCreation
{
    [Required]
    public string SubjectName { get; set; } = string.Empty;
}
