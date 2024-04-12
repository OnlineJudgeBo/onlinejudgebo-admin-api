using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public class ContestsRepository : IContestsRepository
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public ContestsRepository(AppDbContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<IEnumerable<Contest>> GetAllContestsAsync()
    {
        IEnumerable<DbContest> contests = await _context.Contests
        .OrderByDescending(c => c.ContestId)
        .Select(c => new DbContest
        {
            ContestId = c.ContestId,
            Title = c.Title,
            Private = c.Private,
            StartTime = c.StartTime,
            EndTime = c.EndTime,
            Defunct = c.Defunct
        }
        ).ToListAsync();
        return _mapper.Map<IEnumerable<Contest>>(contests);
    }

    public async Task<Contest> GetContestByIdAsync(int contestId)
    {
        DbContest contest = await _context.Contests
        .Where(c => c.ContestId == contestId)
        .Select(c => new DbContest
        {
            ContestId = c.ContestId,
            Title = c.Title,
            Description = c.Description,
            Private = c.Private,
            StartTime = c.StartTime,
            EndTime = c.EndTime,
            Defunct = c.Defunct,
            ContestProblems = c.ContestProblems.Select(c => new DbContestProblem
            {
                Num = c.Num,
                ProblemId = c.ProblemId,
            }).ToList(),
            ContestUsers = c.ContestUsers.Select(cu => new DbContestUser
            {
                ContestId = cu.ContestId,
                UserId = cu.UserId,
                IsOwner = cu.IsOwner,
            }).ToList()
        })
        .FirstOrDefaultAsync();
        return _mapper.Map<Contest>(contest);
    }

    public async Task<Contest> CreateContestAsync(Contest contest)
    {
        DbContest contests = _mapper.Map<DbContest>(contest);
        _context.Contests.Add(contests);
        await _context.SaveChangesAsync();
        return _mapper.Map<Contest>(contests);
    }


}
