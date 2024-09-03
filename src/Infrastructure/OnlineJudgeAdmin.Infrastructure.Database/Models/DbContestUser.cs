using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("contest_user")]
public partial class DbContestUser
{
    [Key]
    [Column("contest_id")]
    public int ContestId { get; set; }
    public DbContest Contest { get; set; }

    [Column("user_id")]
    public string UserId { get; set; }
    public DbUser User { get; set; }

    [Column("site_id")]
    public int SiteId { get; set; }
    public DbSite Site { get; set; }

    [Column("is_owner")]
    public bool IsOwner { get; set; }
}
