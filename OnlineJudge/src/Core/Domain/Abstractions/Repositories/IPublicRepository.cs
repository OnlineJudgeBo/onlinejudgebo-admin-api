using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IPublicRepository
{
    Task<PublicDashboardResponse> GetDashboardAsync(int siteId);

    Task<PublicProblemsResponse> GetProblemsAsync(int siteId, int page, int pageSize, string? searchTerm, int? year, string? contestTrack, string? source, string? tag, string? sortBy, int? contestId, CurrentUser? currentUser = null);

    Task<PublicProblemDetailResponse> GetProblemDetailAsync(int siteId, int problemId);

    Task<PublicProblemDetailResponse> GetContestProblemDetailAsync(int siteId, int contestId, string contestProblemId, CurrentUser? currentUser = null);

    Task<PublicProblemStatisticsResponse> GetProblemStatisticsAsync(int siteId, int problemId);

    Task<PublicProblemFiltersResponse> GetProblemFiltersAsync(int siteId);

    Task<PublicRankingResponse> GetRankingAsync(int siteId, int limit, string? scope);

    Task<PublicTopicsResponse> GetTopicsAsync(int siteId);

    Task<PublicContestsResponse> GetContestsAsync(int siteId, string? status, string? level, string? sortBy, int page, int pageSize, string? searchTerm);

    Task<ContestReportResponse> GetContestReportAsync(int siteId, int contestId, CurrentUser? currentUser = null);

    Task<bool> CanDownloadContestReportCsvAsync(CurrentUser currentUser, int siteId, int contestId);

    Task RegisterForContestAsync(CurrentUser currentUser, int siteId, int contestId);

    Task<IReadOnlyCollection<PublicLanguageItem>> GetLanguagesAsync();

    Task<PublicSubmissionsResponse> GetSubmissionsAsync(int siteId, int page, int pageSize, int? contestId, int? problemId, string? userId, int? languageId, string? statusKey, CurrentUser? currentUser = null);

    Task<PublicSubmissionsResponse> GetOwnSubmissionsAsync(CurrentUser currentUser, int page, int pageSize);

    Task<PublicSubmissionsResponse> GetRecentSubmissionsAsync(int siteId, int page, int pageSize, int? contestId, long? courseId, CurrentUser? currentUser = null);

    Task<PublicOnlineUsersResponse> GetOnlineUsersAsync(int siteId, int windowMinutes);

    Task<IReadOnlyCollection<PublicSubmissionSourceCodeItem>> GetOwnSubmissionSourceCodesAsync(CurrentUser currentUser);

    Task<PublicSubmissionResponse> SubmitAsync(CurrentUser currentUser, PublicSubmissionRequest request, int languageId);

    Task<PublicAuthenticatedUser> LoginAsync(string userOrEmail, string password, int siteId);

    Task<PublicAuthenticatedUser> RegisterAsync(string userId, string passwordHash, string email, string? nick, string? lastName, string? school, int siteId, string ipAddress);

    Task<PublicPasswordRecoveryTarget?> GetPasswordRecoveryTargetAsync(string email, int siteId);

    Task SavePasswordRecoveryTokenAsync(string userId, int siteId, string tokenHash, DateTime expiresAtUtc);

    Task<bool> ResetPasswordWithTokenAsync(string email, int siteId, string tokenHash, DateTime nowUtc, string passwordHash);

    Task<PublicAuthenticatedUser> GetAuthenticatedUserAsync(string userId, int siteId);
}
