namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class News
{
    public int NewsId { get; set; }

    public string UserId { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public DateTime Time { get; set; }

    public sbyte Importance { get; set; }

    public string Defunct { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
