namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class Loginlog
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public string? Password { get; set; }

    public string? Ip { get; set; }

    public DateTime? Time { get; set; }
}
