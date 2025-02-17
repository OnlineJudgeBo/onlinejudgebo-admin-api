using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;
[Table("sites")]
public class DbSite
{
    [Key, Column("site_id")]
    public int SiteId { get; set; }

    [Column("name")]
    public string Name { get; set; }

    public ICollection<DbContestUser> ContestUsers { get; set; }
    public ICollection<DbContestSite> ContestSites { get; set; }
    public ICollection<DbProblemSite> ProblemSites { get; set; }

}
