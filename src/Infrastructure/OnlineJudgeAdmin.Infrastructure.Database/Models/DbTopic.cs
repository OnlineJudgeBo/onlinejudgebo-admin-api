using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("topic")]
public partial class DbTopic
{
    [Key]
    [Column("topic_id")]
    public int TopicId { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    public virtual ICollection<DbClassification>? Classifications { get; set; } = new List<DbClassification>();
}
