using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Core.Domain.Models;

public class Contest
{
    public int ContestId { get; set; }
    public string Title { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Defunct { get; set; }
    public string Description { get; set; }
    public int Private { get; set; }
    public int Langmask { get; set; }
    public bool Obi { get; set; }
}
