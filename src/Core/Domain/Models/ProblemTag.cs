namespace OnlineJudgeAdmin.Core.Domain.Models;

public class ProblemTag
{
    public int ProblemId { get; set; }

    public int TagId { get; set; }

    public virtual Problem? Problem { get; set; }

    public virtual Tag? Tag { get; set; }
}
