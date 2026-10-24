namespace OnlineJudgeAdmin.Core.Domain.Models;

// Points of one subtask of a submission scored by points (IOI style). Written by the judge.
public class SolutionSubtask
{
    public int SolutionId { get; set; }

    public int Subtask { get; set; }

    public decimal Points { get; set; }

    public decimal MaxPoints { get; set; }

    public int Tests { get; set; }
}
