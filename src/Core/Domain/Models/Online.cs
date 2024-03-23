namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class Online
{
    public string Hash { get; set; } = null!;

    public string Ip { get; set; } = null!;

    public string Ua { get; set; } = null!;

    public string? Refer { get; set; }

    public int Lastmove { get; set; }

    public int? Firsttime { get; set; }

    public string? Uri { get; set; }
}
