using FluentValidation;

using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
public class ProgrammingLanguageService : IProgrammingLanguageService
{
    private readonly IProgrammingLanguagesRepository _programmingLanguageRepository;

    private readonly IValidator<Problem> _userValidation;

    public ProgrammingLanguageService(
        IProgrammingLanguagesRepository programmingLanguageRepository,
        IValidator<Problem> ProblemValidation)
    {
        _programmingLanguageRepository = programmingLanguageRepository ?? throw new ArgumentException(null, nameof(programmingLanguageRepository));
        _userValidation = ProblemValidation ?? throw new ArgumentNullException(nameof(ProblemValidation));
    }

    public Task<IEnumerable<ProgrammingLanguage>> GetAllProgrammingLanguageAsync()
    {
        return _programmingLanguageRepository.GetAllProgrammingLanguageAsync();
    }
}
