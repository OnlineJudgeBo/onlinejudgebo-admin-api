using AutoMapper;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public class SolutionRepository : ISolutionRepository
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public SolutionRepository(AppDbContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<int> SaveSolutionAsync(Solution solutionToCreate)
    {
        DbSolution solution = _mapper.Map<DbSolution>(solutionToCreate);

        await _context.Solutions.AddAsync(solution);
        await _context.SaveChangesAsync();
        return solution.SolutionId;
    }
}
