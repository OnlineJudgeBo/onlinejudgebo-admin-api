using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Core.Application.Services.Helpers;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

public class PublicService : IPublicService
{
    private readonly IPublicRepository _publicRepository;
    private readonly IAcademicRepository _academicRepository;
    private readonly IAcademicService _academicService;
    private readonly ISolutionService _solutionService;
    private readonly IPasswordRecoveryEmailService _passwordRecoveryEmailService;
    private readonly IWelcomeEmailService _welcomeEmailService;
    private readonly IConfiguration _configuration;

    public PublicService(
        IPublicRepository publicRepository,
        IAcademicRepository academicRepository,
        IAcademicService academicService,
        ISolutionService solutionService,
        IPasswordRecoveryEmailService passwordRecoveryEmailService,
        IWelcomeEmailService welcomeEmailService,
        IConfiguration configuration)
    {
        _publicRepository = publicRepository ?? throw new ArgumentNullException(nameof(publicRepository));
        _academicRepository = academicRepository ?? throw new ArgumentNullException(nameof(academicRepository));
        _academicService = academicService ?? throw new ArgumentNullException(nameof(academicService));
        _solutionService = solutionService ?? throw new ArgumentNullException(nameof(solutionService));
        _passwordRecoveryEmailService = passwordRecoveryEmailService ?? throw new ArgumentNullException(nameof(passwordRecoveryEmailService));
        _welcomeEmailService = welcomeEmailService ?? throw new ArgumentNullException(nameof(welcomeEmailService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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

        return PublicSubmissionStatusMapper.Map(solution);
    }

    private static bool IsAcademicManager(CurrentUser currentUser)
    {
        return currentUser.Role == UserRolesEnum.Administrador
            || currentUser.Role == UserRolesEnum.Docente
            || currentUser.Role == UserRolesEnum.Auxiliar;
    }

    public Task<PublicAuthenticatedUser> LoginAsync(string userOrEmail, string password, int siteId)
    {
        ValidateSite(siteId);

        if (string.IsNullOrWhiteSpace(userOrEmail) || string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Credenciales inválidas.");
        }

        return _publicRepository.LoginAsync(userOrEmail.Trim(), password, siteId);
    }

    public Task<PublicAuthenticatedUser> RegisterAsync(string userId, string password, string email, string? nick, string? lastName, string? school, int siteId, string ipAddress)
    {
        ValidateSite(siteId);

        if (string.IsNullOrWhiteSpace(userId) || !System.Text.RegularExpressions.Regex.IsMatch(userId, "^[A-Za-z0-9_]{3,20}$"))
        {
            throw new ArgumentException("UserId inválido. Debe tener 3-20 caracteres alfanuméricos o _.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            throw new ArgumentException("La contraseña debe tener al menos 6 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new ArgumentException("Correo electrónico inválido.");
        }

        return RegisterInternalAsync(userId.Trim(), password, email.Trim(), TrimOrNull(nick), TrimOrNull(lastName), TrimOrNull(school), siteId, ipAddress);
    }

    public async Task RequestPasswordRecoveryAsync(string email, int siteId)
    {
        ValidateSite(siteId);

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Correo electrónico requerido.");
        }

        var normalizedEmail = email.Trim();

        if (!normalizedEmail.Contains('@'))
        {
            throw new ArgumentException("Correo electrónico inválido.");
        }

        var target = await _publicRepository.GetPasswordRecoveryTargetAsync(normalizedEmail, siteId);
        if (target == null)
        {
            throw new KeyNotFoundException("No existe una cuenta activa con ese correo.");
        }

        var recoveryCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var tokenTtlMinutes = 30;
        if (int.TryParse(_configuration["Email:PasswordRecovery:TokenTtlMinutes"] ?? _configuration["PasswordRecovery:TokenTtlMinutes"], out var configuredTtlMinutes))
        {
            tokenTtlMinutes = configuredTtlMinutes;
        }

        tokenTtlMinutes = Math.Max(tokenTtlMinutes, 5);
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(tokenTtlMinutes);

        await _publicRepository.SavePasswordRecoveryTokenAsync(target.UserId, siteId, ComputeRecoveryCodeHash(recoveryCode), expiresAtUtc);
        await _passwordRecoveryEmailService.SendRecoveryCodeAsync(target.Email, target.UserId, recoveryCode, target.Nick);
    }

    public async Task ResetPasswordWithRecoveryCodeAsync(string email, string recoveryCode, int siteId)
    {
        ValidateSite(siteId);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(recoveryCode))
        {
            throw new ArgumentException("Correo y código de recuperación son requeridos.");
        }

        var normalizedEmail = email.Trim();
        var normalizedCode = recoveryCode.Trim().ToUpperInvariant();
        if (!normalizedEmail.Contains('@'))
        {
            throw new ArgumentException("Correo electrónico inválido.");
        }

        if (normalizedCode.Length < 6)
        {
            throw new ArgumentException("Código de recuperación inválido.");
        }

        var passwordHash = GeneratePasswordHash(normalizedCode);
        var wasReset = await _publicRepository.ResetPasswordWithTokenAsync(
            normalizedEmail,
            siteId,
            ComputeRecoveryCodeHash(normalizedCode),
            DateTime.UtcNow,
            passwordHash);

        if (!wasReset)
        {
            throw new UnauthorizedAccessException("El código de recuperación es inválido o expiró.");
        }
    }

    public Task<PublicAuthenticatedUser> GetAuthenticatedUserAsync(CurrentUser currentUser)
    {
        ValidateCurrentUser(currentUser);
        return _publicRepository.GetAuthenticatedUserAsync(currentUser.UserId, currentUser.SiteId);
    }

    private static void ValidateSite(int siteId)
    {
        if (siteId <= 0)
        {
            throw new ArgumentException("SiteId inválido.");
        }
    }

    private static void ValidateCurrentUser(CurrentUser currentUser)
    {
        if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.UserId) || currentUser.SiteId <= 0 || currentUser.UserId == "defaultUserId")
        {
            throw new UnauthorizedAccessException("Invalid user context.");
        }
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

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string ComputeRecoveryCodeHash(string recoveryCode)
    {
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(recoveryCode))).ToLowerInvariant();
    }

    private async Task<PublicAuthenticatedUser> RegisterInternalAsync(string userId, string password, string email, string? nick, string? lastName, string? school, int siteId, string ipAddress)
    {
        var passwordHash = GeneratePasswordHash(password);
        var user = await _publicRepository.RegisterAsync(userId, passwordHash, email, nick, lastName, school, siteId, ipAddress);

        try
        {
            await _welcomeEmailService.SendWelcomeAsync(user.Email, user.UserId, user.Nick);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[PublicService] No se pudo enviar el correo de bienvenida. UserId={user.UserId} Email={user.Email} SiteId={siteId}");
            Console.Error.WriteLine(ex.ToString());
        }

        return user;
    }

    private static string GeneratePasswordHash(string password)
    {
        return LegacyPasswordHash.Generate(password);
    }
}
