using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("news")]
public partial class DbNews
{
    [Key]
    [Column("news_id")]
    public int NewsId { get; set; }

    [Column("user_id")]
    public string UserId { get; set; } = null!;

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("content", TypeName = "text")]
    public string Content { get; set; } = null!;

    [Column("time")]
    public DateTime Time { get; set; }

    [Column("importance")]
    public sbyte Importance { get; set; }

    [Column("defunct")]
    public string Defunct { get; set; } = null!;

    public virtual DbUser User { get; set; } = null!;
}
