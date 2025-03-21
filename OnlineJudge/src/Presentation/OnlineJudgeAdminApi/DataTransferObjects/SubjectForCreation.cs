using System.ComponentModel.DataAnnotations;

namespace OnlineJudgeAdminApi.DataTransferObjects;

public partial class SubjectForCreation
{
    [Required]
    public string SubjectName { get; set; }
}
