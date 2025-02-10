using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ScheduleManager.Core.Domain.Abstractions.Repositories;

namespace ScheduleManager.Infrastructure.Database.Implementations;

public class JudgeRepository : IJudgeRepository
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public JudgeRepository(AppDbContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task RejudgeSolutionByIdAsync(int solutionId)
    {
        _context.Database.ExecuteSqlRaw(
            "UPDATE `solution` SET result = 1 WHERE solution_id = {0}", solutionId);
    }

    public async Task RejudgeSolutionByProblemIdAsync(int problemId)
    {
        _context.Database.ExecuteSqlRaw(
            "UPDATE `solution` SET result = 1 WHERE problem_id = {0}", problemId);
    }
}
