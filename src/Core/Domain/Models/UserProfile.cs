namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class UserProfile
{
    public string UserId { get; set; } = null!;

    public string? Email { get; set; }

    public string Nick { get; set; } = null!;

    public string? School { get; set; }

    public string? Lastname { get; set; }

    public int? PaisId { get; set; }

    public string? Ci { get; set; }

    public int Departament { get; set; }

    public string? District { get; set; }

    public virtual User User { get; set; } = null!;
}
