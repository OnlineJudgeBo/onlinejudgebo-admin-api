namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class Solution
{
    public int SolutionId { get; set; }

    public int ProblemId { get; set; }

    public string UserId { get; set; } = null!;

    public int Time { get; set; }

    public int Memory { get; set; }

    public DateTime InDate { get; set; }

    public short Result { get; set; }

    public int Language { get; set; }

    public string Ip { get; set; } = null!;

    public int? ContestId { get; set; }

    public sbyte Valid { get; set; }

    public sbyte Num { get; set; }

    public int CodeLength { get; set; }

    public DateTime? JudgeTime { get; set; }

    public decimal PassRate { get; set; }

    public bool IsRemoteOj { get; set; } = false;

    public int RemoteId { get; set; } = 0;

    public virtual Compileinfo? Compileinfo { get; set; }

    public virtual Problem Problem { get; set; } = null!;

    public virtual Runtimeinfo? Runtimeinfo { get; set; }

    public virtual SourceCode? SourceCode { get; set; }

    public virtual User User { get; set; } = null!;
}
