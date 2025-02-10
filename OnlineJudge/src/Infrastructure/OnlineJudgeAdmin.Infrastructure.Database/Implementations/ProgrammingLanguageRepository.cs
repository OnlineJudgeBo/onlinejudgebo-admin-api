using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ScheduleManager.Core.Domain.Abstractions.Repositories;
using ScheduleManager.Core.Domain.Models;
using ScheduleManager.Infrastructure.Database.Models;

namespace ScheduleManager.Infrastructure.Database.Implementations;

public class ProgrammingLanguageRepository : IProgrammingLanguagesRepository
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public ProgrammingLanguageRepository(AppDbContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<IEnumerable<ProgrammingLanguage>> GetAllProgrammingLanguageAsync()
    {
        IEnumerable<DbProgrammingLanguage> languages = await _context.ProgrammingLanguages
        .Select(c => new DbProgrammingLanguage
        {
            LanguageId = c.LanguageId,
            Name = c.Name
        }
        ).ToListAsync();
        return _mapper.Map<IEnumerable<ProgrammingLanguage>>(languages);
    }
}
