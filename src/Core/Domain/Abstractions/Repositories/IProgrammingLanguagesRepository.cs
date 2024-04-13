using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface IProgrammingLanguagesRepository
{
    public Task<IEnumerable<ProgrammingLanguage>> GetAllProgrammingLanguageAsync();
}
