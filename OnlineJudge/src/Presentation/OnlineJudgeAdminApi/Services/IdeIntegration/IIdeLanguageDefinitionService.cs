using OnlineJudgeAdminApi.DataTransferObjects;

namespace OnlineJudgeAdminApi.Services.IdeIntegration;

public interface IIdeLanguageDefinitionService
{
    Task<IdeLanguageDefinitionDto[]> GetAllowedLanguageDefinitionsAsync(int[] allowedLanguageIds);
}
