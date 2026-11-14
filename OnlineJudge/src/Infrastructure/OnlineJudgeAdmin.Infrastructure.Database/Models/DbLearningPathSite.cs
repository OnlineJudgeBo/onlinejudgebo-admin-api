using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

// Sites where a learning path is shown; the owner site (learning_path.site_id) is the only one that edits it.
[Table("learning_path_site")]
public class DbLearningPathSite
{
    [Column("learning_path_id")]
    public long LearningPathId { get; set; }

    [Column("site_id")]
    public int SiteId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }
}
