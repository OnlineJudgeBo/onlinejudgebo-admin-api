namespace OnlineJudgeAdminApi.DataTransferObjects;

public partial class ContestForUpdate
{
    public string Title { get; set; }
    public string Description { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool IsPrivate { get; set; }

    public virtual ICollection<ProblemForContestCreation> SelectedProblem { get; set; }

    public virtual ICollection<UserForContestCreation> SelectedUser { get; set; }

    public virtual ICollection<ProgrammingLanguageForContestCreation> selectedLanguage { get; set; }
}
