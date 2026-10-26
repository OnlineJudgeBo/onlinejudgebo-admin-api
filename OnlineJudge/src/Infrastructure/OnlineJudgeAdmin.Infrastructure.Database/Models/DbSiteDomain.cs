using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

// The domains that open each site (one site can have several, e.g. with and without www).
[Table("site_domain")]
public class DbSiteDomain
{
    [Key, Column("domain")]
    public string Domain { get; set; } = string.Empty;

    [Column("site_id")]
    public int SiteId { get; set; }

    public DbSite Site { get; set; } = null!;
}
