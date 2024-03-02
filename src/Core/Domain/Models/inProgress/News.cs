using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Core.Domain.Models;


public class News
{
    public int NewsId { get; set; }

    public string? UserId { get; set; }

    public string? Title { get; set; }

    public string? Content { get; set; }

    public DateTime Time { get; set; }

    public int Importance { get; set; }

    public string? Defunct { get; set; }

    public virtual User? User { get; set; }
}
