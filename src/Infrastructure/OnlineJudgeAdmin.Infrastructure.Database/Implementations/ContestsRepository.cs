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
        })
        .Take(100)
        .ToListAsync();
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
        contest.Defunct = "N";

        DbContest newContest = _mapper.Map<DbContest>(contest);

        var dbContestProblems = new List<DbContestProblem>(newContest.ContestProblems);
        var dbContestUsers = new List<DbContestUser>(newContest.ContestUsers);
        var dbProgrammingLanguages = new List<DbProgrammingLanguage>(newContest.ProgrammingLanguages);

        newContest.ContestProblems.Clear();
        newContest.ContestUsers.Clear();
        newContest.ProgrammingLanguages.Clear();

        _context.Contests.Add(newContest);

        await _context.SaveChangesAsync();
        await _context.Contests.FirstOrDefaultAsync(c => c.ContestId == newContest.ContestId);

        foreach (var problem in dbContestProblems)
        {
            var contestProblem = new DbContestProblem
            {
                ContestId = newContest.ContestId,
                ProblemId = problem.ProblemId,
                Num = problem.Num
            };
            _context.ContestProblems.Add(contestProblem);
        }
        //await _context.SaveChangesAsync();

        foreach (var user in dbContestUsers)
        {
            if (user.UserId != null)
            {

                var contestUser = new DbContestUser
                {
                    ContestId = newContest.ContestId,
                    UserId = user.UserId,
                    IsOwner = user.IsOwner

                };
                _context.ContestUsers.Add(contestUser);
            }
        }
        //await _context.SaveChangesAsync();

        foreach (var lang in dbProgrammingLanguages)
        {
            var language = await _context.ProgrammingLanguages.FindAsync(lang.LanguageId);
            if (language != null)
            {
                newContest.ProgrammingLanguages.Add(language);
            }
        }
        await _context.SaveChangesAsync();
        return await GetContestByIdAsync(newContest.ContestId);
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
            if (user.UserId != ownerContest.UserId)
            {
                existingContest.ContestUsers.Add(user);
            }
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
