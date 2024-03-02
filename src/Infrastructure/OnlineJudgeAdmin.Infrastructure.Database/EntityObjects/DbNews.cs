using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

[Table("news")]
public class DbNews
{
    [Key]
    [Column("news_id")]
    public int NewsId { get; set; }

    [Column("user_id")]
    public string UserId { get; set; }

    [Column("title")]
    [MaxLength(200)]
    public string Title { get; set; }

    [Column("content")]
    public string Content { get; set; }

    [Column("time")]
    public DateTime Time { get; set; }

    [Column("importance")]
    public int Importance { get; set; }

    [Column("defunct")]
    [MaxLength(1)]
    public string Defunct { get; set; }

    [ForeignKey("UserId")]
    public virtual DbUser User { get; set; }
}
