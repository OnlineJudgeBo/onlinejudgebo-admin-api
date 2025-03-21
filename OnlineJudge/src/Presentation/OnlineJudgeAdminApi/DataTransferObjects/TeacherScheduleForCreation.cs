using System.ComponentModel.DataAnnotations;

namespace OnlineJudgeAdminApi.DataTransferObjects;

public partial class TeacherScheduleForCreation
{
    [Required]
    public string TeacherName { get; set; }
}
