using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;
[Table("tags")]
public class DbTag
{
    [Key]
    public int TagId { get; set; }

    [Required]
    [MaxLength(100)]
    public string TagName { get; set; }
}

