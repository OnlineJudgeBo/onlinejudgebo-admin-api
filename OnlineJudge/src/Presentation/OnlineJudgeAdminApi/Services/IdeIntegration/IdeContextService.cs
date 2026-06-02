using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;

namespace OnlineJudgeAdminApi.Services.IdeIntegration;

public sealed class IdeContextService : IIdeContextService
{
    private readonly IProblemService _problemService;
    private readonly IUserRepository _userRepository;
    private readonly IIdeLanguageDefinitionService _languageDefinitionService;

    public IdeContextService(
        IProblemService problemService,
        IUserRepository userRepository,
        IIdeLanguageDefinitionService languageDefinitionService)
    {
        _problemService = problemService ?? throw new ArgumentNullException(nameof(problemService));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _languageDefinitionService = languageDefinitionService ?? throw new ArgumentNullException(nameof(languageDefinitionService));
    }

    public async Task<IdeContextBuildResult> BuildContextAsync(IdeLaunchClaims claims, IdeContextRequest request)
    {
        var user = await _userRepository.GetUserById(claims.UserId, claims.SiteId);
        if (user is null)
        {
            return IdeContextBuildResult.UserNotAllowed();
        }

        var problem = await _problemService.GetProblemByIdAsync(claims.ProblemId);
        if (problem?.ProblemId is null)
        {
            return IdeContextBuildResult.ProblemNotFound();
        }

        var allowedLanguages = ResolveAllowedLanguages(claims, request.LanguageId);
        var languages = await _languageDefinitionService.GetAllowedLanguageDefinitionsAsync(allowedLanguages);

        return IdeContextBuildResult.Success(new IdeContextResponse(
            ToProblemContext(problem, claims.ProblemId),
            allowedLanguages,
            languages,
            new IdeContextIdentifiersDto(
                claims.ProblemId,
                claims.ContestId,
                claims.Num,
                claims.UserId,
                claims.SiteId,
                request.LanguageId,
                request.LanguageName,
                request.Handoff)));
    }

    private static int[] ResolveAllowedLanguages(IdeLaunchClaims claims, int? requestedLanguageId)
    {
        if (claims.AllowedLanguages.Length > 0)
        {
            return claims.AllowedLanguages;
        }

        return requestedLanguageId.HasValue && requestedLanguageId.Value > 0
            ? new[] { requestedLanguageId.Value }
            : Array.Empty<int>();
    }

    private static IdeProblemContextDto ToProblemContext(Problem problem, int fallbackProblemId)
    {
        return new IdeProblemContextDto(
            problem.ProblemId!.Value.ToString(),
            problem.Title ?? $"Problema {fallbackProblemId}",
            problem.Description ?? string.Empty,
            problem.Input ?? string.Empty,
            problem.Output ?? string.Empty,
            string.Empty,
            problem.Hint ?? string.Empty,
            new IdeProblemExampleDto(problem.SampleInput ?? string.Empty, problem.SampleOutput ?? string.Empty),
            problem.TimeLimit.HasValue ? $"{problem.TimeLimit}s" : null,
            problem.MemoryLimit.HasValue ? $"{problem.MemoryLimit} MB" : null);
    }
}
