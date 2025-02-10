using System.ComponentModel.DataAnnotations.Schema;

namespace ScheduleManager.Infrastructure.Database.Models
{
    [Table("contest_site")]
    public class DbContestSite
    {
        [Column("contest_id")]
        public int ContestId { get; set; }

        [Column("site_id")]
        public int SiteId { get; set; }

        [ForeignKey("ContestId")]
        public virtual DbContest Contest { get; set; } = null!;

        [ForeignKey("SiteId")]
        public virtual DbSite Site { get; set; } = null!;
    }
}
