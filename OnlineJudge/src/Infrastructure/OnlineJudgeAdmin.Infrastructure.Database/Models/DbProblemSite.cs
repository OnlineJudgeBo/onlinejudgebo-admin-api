using System.ComponentModel.DataAnnotations.Schema;

namespace ScheduleManager.Infrastructure.Database.Models
{
    [Table("problems_site")]
    public class DbProblemSite
    {
        [Column("problem_id")]
        public int problemId { get; set; }

        [Column("site_id")]
        public int SiteId { get; set; }

        [ForeignKey("problemId")]
        public virtual DbProblem Problem { get; set; } = null!;

        [ForeignKey("SiteId")]
        public virtual DbSite Site { get; set; } = null!;
    }
}
