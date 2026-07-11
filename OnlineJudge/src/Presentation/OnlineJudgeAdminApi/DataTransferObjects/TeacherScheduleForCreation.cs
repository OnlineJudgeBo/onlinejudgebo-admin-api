using System.ComponentModel.DataAnnotations;

namespace OnlineJudgeAdminApi.DataTransferObjects;

public class TeacherScheduleForCreation
{
    [Required]
    public string TeacherName { get; set; } = string.Empty;
}
