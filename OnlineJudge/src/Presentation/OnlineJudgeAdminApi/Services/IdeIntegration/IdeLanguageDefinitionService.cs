using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;

namespace OnlineJudgeAdminApi.Services.IdeIntegration;

public sealed class IdeLanguageDefinitionService : IIdeLanguageDefinitionService
{
    private readonly IProgrammingLanguageService _programmingLanguageService;

    public IdeLanguageDefinitionService(IProgrammingLanguageService programmingLanguageService)
    {
        _programmingLanguageService = programmingLanguageService ?? throw new ArgumentNullException(nameof(programmingLanguageService));
    }

    public async Task<IdeLanguageDefinitionDto[]> GetAllowedLanguageDefinitionsAsync(int[] allowedLanguageIds)
    {
        var allowed = allowedLanguageIds.ToHashSet();
        var allLanguages = await _programmingLanguageService.GetAllProgrammingLanguageAsync();
        return allLanguages
            .Where(language => language.LanguageId.HasValue)
            .Where(language => allowed.Count == 0 || allowed.Contains(language.LanguageId!.Value))
            .Select(language => new IdeLanguageDefinitionDto(
                language.LanguageId!.Value,
                language.Name ?? string.Empty,
                ToIdeLanguage(language)))
            .ToArray();
    }

    private static string ToIdeLanguage(ProgrammingLanguage language)
    {
        var name = NormalizeLanguageName(language.Name);
        if (name is "c" || name.Contains("c++") || name.Contains("cpp")) return "cpp";
        if (name.Contains("python")) return "python";
        if (name.Contains("java") && !name.Contains("script")) return "java";
        if (name is "go" || name.Contains("golang")) return "go";
        if (name.Contains("javascript") || name.Contains("node")) return "javascript";
        if (name.Contains("rust")) return "rust";
        return ToIdeLanguageId(name);
    }

    private static string NormalizeLanguageName(string? name)
    {
        return (name ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static string ToIdeLanguageId(string normalizedName)
    {
        var id = new string(normalizedName
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray())
            .Trim('-');

        return string.IsNullOrWhiteSpace(id) ? "text" : id;
    }
}
