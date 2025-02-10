namespace ScheduleManager.Core.Domain.Models;

public partial class UserSetting
{
    public string UserId { get; set; } = null!;

    public int Volume { get; set; }

    public int Language { get; set; }

    public int? Obi { get; set; }

    public int InstitucionId { get; set; }

    public string? Vcyt { get; set; }

    public string? Rude { get; set; }

    public int? Level { get; set; }

    public virtual User User { get; set; } = null!;
}
