using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

[Table("tags")]
public class DbTag
{
    [Key]
    [Column("tag_id")]
    public int TagId { get; set; }

    [Required]
    [Column("tag_name")]
    [MaxLength(100)]
    public string TagName { get; set; }

    public virtual ICollection<DbProblem> Problems { get; set; }

    public DbTag()
    {
        Problems = new HashSet<DbProblem>();
    }
}
