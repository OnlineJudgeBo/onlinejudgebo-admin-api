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
}
