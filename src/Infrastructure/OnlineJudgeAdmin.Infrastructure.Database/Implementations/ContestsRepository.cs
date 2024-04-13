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
            Langmask = c.Langmask,
            ProgrammingLanguages = c.ProgrammingLanguages.Select(c => new DbProgrammingLanguage
            {
                LanguageId = c.LanguageId,
                Name = c.Name
            }).ToList(),
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

    public async Task<Contest> UpdateContestAsync(int contestId, Contest contest)
    {
        contest.Defunct = "N";
        DbContest contestsToUpdate = _mapper.Map<DbContest>(contest);

        var existingContest = await _context.Contests
            .Include(c => c.ContestProblems)
            .Include(c => c.ContestUsers)
            .Include(c => c.ProgrammingLanguages)
            .FirstOrDefaultAsync(c => c.ContestId == contestId);

        if (existingContest == null)
            throw new KeyNotFoundException("Contest not found with ID: " + contestId);

        var ownerContest = existingContest.ContestUsers.Where(p => p.IsOwner).First();

        _context.Entry(existingContest).CurrentValues.SetValues(contest);

        existingContest.ContestProblems.Clear();
        foreach (var problem in contestsToUpdate.ContestProblems)
        {
            existingContest.ContestProblems.Add(problem);
        }

        existingContest.ContestUsers.Clear();
        existingContest.ContestUsers.Add(ownerContest);
        foreach (var user in contestsToUpdate.ContestUsers)
        {
            existingContest.ContestUsers.Add(user);
        }

        existingContest.ProgrammingLanguages.Clear();
        foreach (var langId in contestsToUpdate.ProgrammingLanguages)
        {
            var language = await _context.ProgrammingLanguages.FindAsync(langId.LanguageId);
            if (language != null)
            {
                _context.Entry(language).State = EntityState.Unchanged;
                existingContest.ProgrammingLanguages.Add(language);
            }
        }

        await _context.SaveChangesAsync();
        return await GetContestByIdAsync(contestId);
    }
}
