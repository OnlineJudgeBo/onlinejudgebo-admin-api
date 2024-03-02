using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public class ProblemRepository : IProblemRepository
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public ProblemRepository(AppDbContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<IEnumerable<Problem>> GetAllProblemsAsync()
    {
        var dbProblems = await _context.problems
        .OrderByDescending(q => q.ProblemId)
        .ToListAsync();
        return _mapper.Map<IEnumerable<Problem>>(dbProblems);
    }
}
