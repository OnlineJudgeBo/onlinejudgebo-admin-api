namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

public partial class DbMonthlySubmission
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int TotalSubmissions { get; set; }
}
