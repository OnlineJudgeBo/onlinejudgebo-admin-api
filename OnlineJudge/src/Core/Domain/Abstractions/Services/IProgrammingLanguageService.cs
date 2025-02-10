using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Domain.Abstractions.Services;

public interface IProgrammingLanguageService
{
    public Task<IEnumerable<ProgrammingLanguage>> GetAllProgrammingLanguageAsync();
}

