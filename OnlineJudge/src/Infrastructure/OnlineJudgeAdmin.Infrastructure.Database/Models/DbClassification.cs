using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace ScheduleManager.Infrastructure.Database.Models;

[Table("classification")]
public partial class DbClassification
{
    [Key]
    [Column("classification_id")]
    public int ClassificationId { get; set; }

    [Column("topic_id")]
    public int TopicId { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    public virtual DbTopic? Topic { get; set; } = null!;

    public virtual ICollection<DbProblem>? Problems { get; set; }
}
