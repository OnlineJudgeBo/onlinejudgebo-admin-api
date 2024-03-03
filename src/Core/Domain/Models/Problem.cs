using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineJudgeAdmin.Core.Domain.Models;

public class Problem
{
    public int ProblemId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Input { get; set; }
    public string? Output { get; set; }
    public string? SampleInput { get; set; }
    public string? SampleOutput { get; set; }
    public string? Spj { get; set; }
    public string? Hint { get; set; }
    public string? Source { get; set; }
    public DateTime? InDate { get; set; }
    public int TimeLimit { get; set; }
    public int MemoryLimit { get; set; }
    public string? Defunct { get; set; }
    public int Accepted { get; set; }
    public int Submit { get; set; }
    public int Solved { get; set; }
    public virtual ICollection<Tag>? Tags { get; set; }
}
