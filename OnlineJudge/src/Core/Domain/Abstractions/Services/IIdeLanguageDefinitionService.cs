using OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IIdeLanguageDefinitionService
{
    Task<IdeLanguageDefinition[]> GetAllowedLanguageDefinitionsAsync(int[] allowedLanguageIds);
}
