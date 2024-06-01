using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

[Table("solution_client")]
public partial class DbSolutionClient
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("solution_id")]
    [Required]
    public int SolutionId { get; set; }

    [Column("client_id")]
    [Required]
    public int ClientId { get; set; }

    [Column("assigned_date")]
    [Required]
    public DateTime AssignedDate { get; set; }
}
