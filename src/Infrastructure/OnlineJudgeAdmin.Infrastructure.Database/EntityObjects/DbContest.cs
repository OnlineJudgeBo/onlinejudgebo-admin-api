using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Infrastructure.Database.EntityObjects;

[Table("contest")]
public class DbContest
{
    [Key]
    [Column("contest_id")]
    public int ContestId { get; set; }

    [Column("title")]
    public string? Title { get; set; }

    [Column("start_time")]
    public DateTime? StartTime { get; set; }

    [Column("end_time")]
    public DateTime? EndTime { get; set; }

    [Column("defunct")]
    public string? Defunct { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("private")]
    public int Private { get; set; }

    [Column("langmask")]
    public int Langmask { get; set; }

    [Column("obi")]
    public bool Obi { get; set; }
}
