namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class Privilege
{
    public int PrivilegeId { get; set; }

    public string UserId { get; set; } = null!;

    public string Rightstr { get; set; } = null!;

    public string Defunct { get; set; } = null!;
}
