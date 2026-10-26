using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Core.Application.Services.Helpers;
using static OnlineJudgeAdmin.Core.Application.Services.Helpers.PublicRequestRules;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

public class PublicService : IPublicService
{
    private readonly IPublicRepository _publicRepository;
    private readonly IAcademicRepository _academicRepository;
    private readonly IAcademicService _academicService;
    private readonly ISolutionService _solutionService;

    public PublicService(
        IPublicRepository publicRepository,
        IAcademicRepository academicRepository,
        IAcademicService academicService,
        ISolutionService solutionService)
    {
        _publicRepository = publicRepository ?? throw new ArgumentNullException(nameof(publicRepository));
        _academicRepository = academicRepository ?? throw new ArgumentNullException(nameof(academicRepository));
        _academicService = academicService ?? throw new ArgumentNullException(nameof(academicService));
        _solutionService = solutionService ?? throw new ArgumentNullException(nameof(solutionService));
    }

    public Task<PublicDashboardResponse> GetDashboardAsync(int siteId)
    {
        ValidateSite(siteId);
        return _publicRepository.GetDashboardAsync(siteId);
    }

    public Task<PublicProblemsResponse> GetProblemsAsync(int siteId, int page, int pageSize, string? searchTerm, int? year, string? contestTrack, string? source, string? tag, string? sortBy, int? contestId, CurrentUser? currentUser = null)
    {
        ValidateSite(siteId);
        ValidateOptionalPositiveId(contestId, "ContestId");

        return _publicRepository.GetProblemsAsync(siteId, Math.Max(page, 1), Clamp(pageSize, 1, 100), searchTerm, year, contestTrack, source, tag, sortBy, contestId, currentUser);
    }

    public Task<PublicProblemDetailResponse> GetProblemDetailAsync(int siteId, int problemId)
    {
        ValidateSite(siteId);
        ValidatePositiveId(problemId, "ProblemId");

        return _publicRepository.GetProblemDetailAsync(siteId, problemId);
    }

    public Task<PublicProblemDetailResponse> GetContestProblemDetailAsync(int siteId, int contestId, string contestProblemId, CurrentUser? currentUser = null)
    {
        ValidateSite(siteId);
        ValidatePositiveId(contestId, "ContestId");

        if (string.IsNullOrWhiteSpace(contestProblemId))
        {
            throw new ArgumentException("ContestProblemId inválido.");
        }

        return _publicRepository.GetContestProblemDetailAsync(siteId, contestId, contestProblemId, currentUser);
    }

    public Task<PublicProblemStatisticsResponse> GetProblemStatisticsAsync(int siteId, int problemId)
    {
        ValidateSite(siteId);
        ValidatePositiveId(problemId, "ProblemId");

        return _publicRepository.GetProblemStatisticsAsync(siteId, problemId);
    }

    public Task<PublicProblemFiltersResponse> GetProblemFiltersAsync(int siteId)
    {
        ValidateSite(siteId);
        return _publicRepository.GetProblemFiltersAsync(siteId);
    }

    public Task<PublicRankingResponse> GetRankingAsync(int siteId, int limit, string? scope)
    {
        ValidateSite(siteId);
        return _publicRepository.GetRankingAsync(siteId, Clamp(limit, 1, 200), scope);
    }

    public Task<PublicTopicsResponse> GetTopicsAsync(int siteId)
    {
        ValidateSite(siteId);
        return _publicRepository.GetTopicsAsync(siteId);
    }

    public Task<PublicContestsResponse> GetContestsAsync(int siteId, string? status, string? level, string? sortBy, int page, int pageSize, string? searchTerm)
    {
        ValidateSite(siteId);
        return _publicRepository.GetContestsAsync(siteId, status, level, sortBy, Math.Max(page, 1), Clamp(pageSize, 1, 100), searchTerm);
    }

    public Task<ContestReportResponse> GetContestReportAsync(int siteId, int contestId, CurrentUser? currentUser = null)
    {
        ValidateSite(siteId);
        ValidatePositiveId(contestId, "ContestId");

        return _publicRepository.GetContestReportAsync(siteId, contestId, currentUser);
    }

    public Task<bool> CanDownloadContestReportCsvAsync(CurrentUser currentUser, int siteId, int contestId)
    {
        ValidateCurrentUser(currentUser);
        ValidateSite(siteId);
        ValidatePositiveId(contestId, "ContestId");

        return _publicRepository.CanDownloadContestReportCsvAsync(currentUser, siteId, contestId);
    }

    public Task RegisterForContestAsync(CurrentUser currentUser, int siteId, int contestId)
    {
        ValidateCurrentUser(currentUser);
        ValidateSite(siteId);
        ValidatePositiveId(contestId, "ContestId");

        return _publicRepository.RegisterForContestAsync(currentUser, siteId, contestId);
    }

    public Task<IReadOnlyCollection<PublicLanguageItem>> GetLanguagesAsync()
    {
        return _publicRepository.GetLanguagesAsync();
    }

    public async Task<PublicSiteResponse> GetSiteByHostAsync(string? host)
    {
        // "Juez.Example.bo:443" and "juez.example.bo." are the same domain.
        var domain = (host ?? string.Empty).Trim().ToLowerInvariant().Split(':')[0].TrimEnd('.');
        var site = domain.Length is > 0 and <= 255 ? await _publicRepository.GetSiteByDomainAsync(domain) : null;
        return site ?? throw new KeyNotFoundException("No hay un sitio para este dominio.");
    }

    public Task<PublicSubmissionsResponse> GetSubmissionsAsync(int siteId, int page, int pageSize, int? contestId, int? problemId, string? userId, int? languageId, string? statusKey, CurrentUser? currentUser = null)
    {
        ValidateSite(siteId);
        ValidateOptionalPositiveId(contestId, "ContestId");
        ValidateOptionalPositiveId(problemId, "ProblemId");

        return _publicRepository.GetSubmissionsAsync(
            siteId,
            Math.Max(page, 1),
            Clamp(pageSize, 1, 100),
            contestId,
            problemId,
            string.IsNullOrWhiteSpace(userId) ? null : userId.Trim(),
            languageId,
            statusKey,
            currentUser);
    }

    public Task<PublicSubmissionsResponse> GetOwnSubmissionsAsync(CurrentUser currentUser, int page, int pageSize)
    {
        ValidateCurrentUser(currentUser);
        return _publicRepository.GetOwnSubmissionsAsync(currentUser, Math.Max(page, 1), Clamp(pageSize, 1, 500));
    }

    public Task<PublicSubmissionsResponse> GetRecentSubmissionsAsync(int siteId, int page, int pageSize, int? contestId, long? courseId, CurrentUser? currentUser = null)
    {
        ValidateSite(siteId);
        ValidateOptionalPositiveId(contestId, "ContestId");

        if (courseId.HasValue && courseId.Value <= 0)
        {
            throw new ArgumentException("CourseId inválido.");
        }

        return _publicRepository.GetRecentSubmissionsAsync(siteId, Math.Max(page, 1), Clamp(pageSize, 1, 100), contestId, courseId, currentUser);
    }

    public Task<PublicOnlineUsersResponse> GetOnlineUsersAsync(int siteId, int windowMinutes)
    {
        ValidateSite(siteId);
        return _publicRepository.GetOnlineUsersAsync(siteId, Clamp(windowMinutes, 1, 1440));
    }

    public Task<IReadOnlyCollection<PublicSubmissionSourceCodeItem>> GetOwnSubmissionSourceCodesAsync(CurrentUser currentUser)
    {
        ValidateCurrentUser(currentUser);
        return _publicRepository.GetOwnSubmissionSourceCodesAsync(currentUser);
    }

    public async Task<PublicSubmissionResponse> SubmitAsync(CurrentUser currentUser, PublicSubmissionRequest request)
    {
        ValidateCurrentUser(currentUser);
        bool hasProblemId = request.ProblemId.HasValue && request.ProblemId.Value > 0;
        bool hasContestProblem = request.ContestId.HasValue
            && (!string.IsNullOrWhiteSpace(request.ContestProblemId)
                || request.Num.HasValue && request.Num.Value >= 0);

        if (!hasProblemId && !hasContestProblem)
        {
            throw new ArgumentException("ProblemId, ContestProblemId o Num es requerido.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceCode) || request.SourceCode.Trim().Length < 5)
        {
            throw new ArgumentException("El código fuente es demasiado corto.");
        }

        if (request.SourceCode.Length > 200000)
        {
            throw new ArgumentException("El código fuente es demasiado largo.");
        }

        if (!request.LanguageId.HasValue || request.LanguageId.Value <= 0)
        {
            throw new ArgumentException("LanguageId es requerido.");
        }

        if (request.CourseId.HasValue || request.AssignmentId.HasValue)
        {
            if (!hasProblemId)
            {
                throw new ArgumentException("ProblemId es requerido para envíos académicos.");
            }

            AcademicSubmissionResponse academicResponse = await _academicService.SubmitAsync(currentUser, new AcademicSubmissionRequest
            {
                ProblemId = request.ProblemId!.Value,
                SourceCode = request.SourceCode,
                LanguageId = request.LanguageId.Value,
                ContestId = request.ContestId,
                CourseId = request.CourseId,
                AssignmentId = request.AssignmentId,
                FileName = request.FileName,
                ClientIp = request.ClientIp
            });

            return new PublicSubmissionResponse
            {
                SolutionId = academicResponse.SolutionId,
                LanguageId = request.LanguageId.Value,
                AutoDetected = false,
                CreatedAtUtc = academicResponse.CreatedAtUtc
            };
        }

        return await _publicRepository.SubmitAsync(currentUser, request, request.LanguageId.Value);
    }

    public async Task<PublicSubmissionStatusResponse> GetSubmissionStatusAsync(CurrentUser currentUser, int solutionId)
    {
        ValidateCurrentUser(currentUser);
        ValidatePositiveId(solutionId, "SolutionId");

        var solution = await _solutionService.GetSolutionByIdAsync(solutionId);
        if (solution == null || solution.SiteId != currentUser.SiteId)
        {
            throw new KeyNotFoundException("No se encontró el envío solicitado.");
        }

        var isOwner = string.Equals(solution.UserId, currentUser.UserId, StringComparison.OrdinalIgnoreCase);
        var canViewCourseSubmissionSource = !isOwner
            && await _academicRepository.CanViewCourseSubmissionSourceAsync(
                currentUser.SiteId,
                solutionId,
                currentUser.UserId,
                IsAcademicManager(currentUser));

        if (!isOwner && !canViewCourseSubmissionSource)
        {
            throw new KeyNotFoundException("No se encontró el envío solicitado.");
        }

        var response = PublicSubmissionStatusMapper.Map(solution);
        // Same rule as the PHP status page: in IOI style only staff see the failed test; a compile error is always shown.
        if (response.IsScored && !IsAcademicManager(currentUser) && solution.Result != JudgeResultCodes.CompileError)
        {
            response.RuntimeMessage = null;
        }
        response.UsesChecker = await _publicRepository.ProblemUsesCheckerAsync(solution.ProblemId);
        return response;
    }

    private static bool IsAcademicManager(CurrentUser currentUser)
    {
        return currentUser.Role == UserRolesEnum.Administrador
            || currentUser.Role == UserRolesEnum.Docente
            || currentUser.Role == UserRolesEnum.Auxiliar;
    }

    private static int Clamp(int value, int min, int max)
    {
        return Math.Min(max, Math.Max(min, value));
    }

    private static void ValidatePositiveId(int value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentException($"{name} inválido.");
        }
    }

    private static void ValidateOptionalPositiveId(int? value, string name)
    {
        if (value.HasValue)
        {
            ValidatePositiveId(value.Value, name);
        }
    }
}
