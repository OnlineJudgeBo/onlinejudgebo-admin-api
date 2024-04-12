namespace OnlineJudgeAdminApi.DataTransferObjects;

public partial class ContestForCreation
{
    public string Title { get; set; }
    public string Description { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool IsPrivate { get; set; }

    public string SelectedLanguage { get; set; }
    public virtual ICollection<ProblemForContestCreation> SelectedProblem { get; set; }
    public virtual ICollection<UserForContestCreation> SelectedUser { get; set; }
}
