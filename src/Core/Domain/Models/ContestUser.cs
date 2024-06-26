namespace OnlineJudgeAdmin.Core.Domain.Models;

public partial class ContestUser
{
    public int ContestId { get; set; }

    public Contest Contest { get; set; }

    public string UserId { get; set; }

    public User User { get; set; }

    public bool IsOwner { get; set; }
}
