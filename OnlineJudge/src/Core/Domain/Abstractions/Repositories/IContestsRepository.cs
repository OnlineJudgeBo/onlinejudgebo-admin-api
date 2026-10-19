using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IContestsRepository
{
    public Task<IEnumerable<Contest>> GetContestsByUserIdDocenteRoleAsync(string userId, bool showAllContest, int siteId, bool includePromoted = false, bool includeDeleted = false);
    public Task<IEnumerable<Contest>> GetContestsByAuxiliarRoleAsync(string userId, int siteId, bool includePromoted = false, bool includeDeleted = false);
    public Task<Contest> CreateContestAsync(Contest contest, int site_id);
    public Task<Contest> GetContestByIdAsync(int contestId, int siteId);
    public Task<Contest> UpdateContestAsync(int contestId, Contest contest, int siteId);
    public Task<Contest> PromoteContestAsync(int contestId, int siteId);
    public Task SetContestDefunctAsync(int contestId, int siteId, string defunct);
    public Task<ExamActivity> GetExamActivityAsync(int contestId, int siteId, DateTime start, DateTime end);
    public Task<Contest?> GetActiveExamForUserAsync(string userId, int siteId, DateTime now);
    public Task<IReadOnlyCollection<ContestSimilarityItem>> GetSimilarityAsync(int contestId, int siteId);
    public Task<IReadOnlyCollection<ContestSubmissionExportItem>> GetSubmissionsForExportAsync(int contestId, int siteId);
}
