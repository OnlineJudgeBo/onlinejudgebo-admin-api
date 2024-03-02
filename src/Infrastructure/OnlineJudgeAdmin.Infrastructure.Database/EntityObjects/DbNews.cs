using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

[Table("news")]

public class DbNews
{
    [Key]
    public int NewsId { get; set; }

    [Required]
    public string UserId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; }

    [Required]
    public string Content { get; set; }

    public DateTime Time { get; set; }

    public int Importance { get; set; }

    [Required]
    [MaxLength(1)]
    public string Defunct { get; set; }

    [ForeignKey("UserId")]
    public virtual DbUser User { get; set; }
}
