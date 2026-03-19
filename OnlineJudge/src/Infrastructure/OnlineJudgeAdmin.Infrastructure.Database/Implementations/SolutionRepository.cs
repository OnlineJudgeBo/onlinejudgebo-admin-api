using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

public class SolutionRepository : ISolutionRepository
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public SolutionRepository(AppDbContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Solution?> GetSolutionByIdAsync(int solutionId)
    {
        var solution = await _context.Solutions
            .AsNoTracking()
            .Include(item => item.Compileinfo)
            .Include(item => item.Runtimeinfo)
            .Include(item => item.SourceCode)
            .Include(item => item.User)
            .ThenInclude(item => item!.UserProfile)
            .FirstOrDefaultAsync(item => item.SolutionId == solutionId);

        return _mapper.Map<Solution?>(solution);
    }

    public async Task<int> SaveSolutionAsync(Solution solutionToCreate)
    {
        DbSolution solution = _mapper.Map<DbSolution>(solutionToCreate);
        await _context.Solutions.AddAsync(solution);
        await _context.SaveChangesAsync();
        return solution.SolutionId;
    }

    public async Task UpdateSolutionRemoteAsync(Solution solutionToCreate)
    {
        DbSolution solution = _mapper.Map<DbSolution>(solutionToCreate);
        _context.Solutions.Attach(solution);
        _context.Entry(solution).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        _context.Entry(solution).State = EntityState.Detached;
    }
}
