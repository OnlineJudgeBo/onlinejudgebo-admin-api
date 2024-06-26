using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("remote_clients")]
public partial class DbRemoteClient
{
    [Key]
    [Column("client_id")]
    public int ClientId { get; set; }

    [Column("callback_url")]
    [StringLength(255)]
    public string CallbackUrl { get; set; }

    [Column("token")]
    public string Token { get; set; }

    [Column("is_available")]
    public bool IsAvailable { get; set; }
}
