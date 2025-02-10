using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface IProgrammingLanguageService
{
    public Task<IEnumerable<ProgrammingLanguage>> GetAllProgrammingLanguageAsync();
}

