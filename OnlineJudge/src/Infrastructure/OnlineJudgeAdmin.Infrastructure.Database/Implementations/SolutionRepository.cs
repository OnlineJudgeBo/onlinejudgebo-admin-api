using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ScheduleManager.Core.Domain.Abstractions.Repositories;
using ScheduleManager.Core.Domain.Models;
using ScheduleManager.Infrastructure.Database.Models;

namespace ScheduleManager.Infrastructure.Database.Implementations;

public class SolutionRepository : ISolutionRepository
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public SolutionRepository(AppDbContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Solution> GetSolutionByIdAsync(int solutionId)
    {
        DbSolution solution = await _context.Solutions.FirstOrDefaultAsync(s => s.SolutionId == solutionId);
        if (solution != null)
        {
            _context.Entry(solution).State = EntityState.Detached;
        }
        return _mapper.Map<Solution>(solution);
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
