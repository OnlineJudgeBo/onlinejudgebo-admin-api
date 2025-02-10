using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Domain.Abstractions.Repositories;

public interface IProgrammingLanguagesRepository
{
    public Task<IEnumerable<ProgrammingLanguage>> GetAllProgrammingLanguageAsync();
}
