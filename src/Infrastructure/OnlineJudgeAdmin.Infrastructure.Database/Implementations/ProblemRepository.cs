using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

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

    public async Task<IEnumerable<Problem>> GetAllProblemsAsync(int siteId)
    {
        IEnumerable<DbProblem> problems = await _context.Problems
            .Where(c => c.ProblemSites.Any(site => site.SiteId == siteId))
            .Where(p => p.Defunct == "N")
            .OrderByDescending(p => p.ProblemId)
            .Select(p => new DbProblem
            {
                ProblemId = p.ProblemId,
                Title = p.Title,
                Source = p.Source,
                InDate = p.InDate,
                Submit = p.Submit,
                Accepted = p.Accepted,
                Classifications = p.Classifications.Select(t => new DbClassification
                {
                    Name = t.Name,
                    Topic = new DbTopic
                    {
                        Name = t.Topic.Name
                    }
                }).ToList(),
                ContestProblems = p.ContestProblems.Select(c => new DbContestProblem
                {
                    Num = c.Num,
                    Contest = new DbContest
                    {
                        Title = c.Contest.Title,
                        EndTime = c.Contest.EndTime
                    }
                }).ToList()
            }).ToListAsync();
        return _mapper.Map<IEnumerable<Problem>>(problems);
    }

    public async Task<IEnumerable<Problem>> GetAllProblemsForAdminAsync(int siteId)
    {
        IEnumerable<DbProblem> problems = await _context.Problems
            .Where(c => c.ProblemSites.Any(site => site.SiteId == siteId))
            .OrderByDescending(p => p.ProblemId)
            .Select(p => new DbProblem
            {
                ProblemId = p.ProblemId,
                Title = p.Title,
                Source = p.Source,
                InDate = p.InDate,
                Submit = p.Submit,
                Accepted = p.Accepted,
                Classifications = p.Classifications.Select(t => new DbClassification
                {
                    Name = t.Name,
                    Topic = new DbTopic
                    {
                        Name = t.Topic.Name
                    }
                }).ToList(),
                ContestProblems = p.ContestProblems.Select(c => new DbContestProblem
                {
                    Num = c.Num,
                    Contest = new DbContest
                    {
                        Title = c.Contest.Title,
                        EndTime = c.Contest.EndTime
                    }
                }).ToList()
            }).ToListAsync();
        return _mapper.Map<IEnumerable<Problem>>(problems);
    }

    public async Task<IEnumerable<Problem>> SearchProblemForAdminAsync(string searchTerm)
    {
        IQueryable<DbProblem> query = _context.Problems
            .OrderBy(p => p.ProblemId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(u =>
                u.Title.Contains(searchTerm) ||
                u.ProblemId.ToString().Contains(searchTerm));
        }

        IEnumerable<DbProblem> users = await query
            .Select(po => new DbProblem
            {
                ProblemId = po.ProblemId,
                Title = po.Title,
                Classifications = po.Classifications.Select(t => new DbClassification
                {
                    Name = t.Name,
                    ClassificationId = t.ClassificationId,
                    Topic = new DbTopic
                    {
                        TopicId = t.Topic.TopicId,
                        Name = t.Topic.Name
                    }
                }).ToList(),
            }).ToListAsync();

        return _mapper.Map<IEnumerable<Problem>>(users);
    }

    public async Task<IEnumerable<Problem>> SearchProblemAsync(string searchTerm)
    {
        IQueryable<DbProblem> query = _context.Problems
            .Where(p => p.Defunct == "N")
            .OrderBy(p => p.ProblemId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(u =>
                u.Title.Contains(searchTerm) ||
                u.ProblemId.ToString().Contains(searchTerm));
        }

        IEnumerable<DbProblem> users = await query
            .Select(po => new DbProblem
            {
                ProblemId = po.ProblemId,
                Title = po.Title,
                Classifications = po.Classifications.Select(t => new DbClassification
                {
                    Name = t.Name,
                    ClassificationId = t.ClassificationId,
                    Topic = new DbTopic
                    {
                        TopicId = t.Topic.TopicId,
                        Name = t.Topic.Name
                    }
                }).ToList(),
            }).ToListAsync();

        return _mapper.Map<IEnumerable<Problem>>(users);
    }

    public async Task<Problem> GetProblemByIdAsync(int problemId)
    {
        DbProblem? problem = await _context.Problems
            .Where(p => p.ProblemId == problemId)
            .Select(p => new DbProblem
            {
                ProblemId = p.ProblemId,
                Title = p.Title,
                Description = p.Description,
                Input = p.Input,
                Output = p.Output,
                SampleInput = p.SampleInput,
                SampleOutput = p.SampleOutput,
                TimeLimit = p.TimeLimit,
                MemoryLimit = p.MemoryLimit,
                Source = p.Source,
                InDate = p.InDate,
                Submit = p.Submit,
                Accepted = p.Accepted,
                Hint = p.Hint,
                Classifications = p.Classifications.Select(t => new DbClassification
                {
                    Name = t.Name,
                    ClassificationId = t.ClassificationId,
                    Topic = new DbTopic
                    {
                        TopicId = t.Topic.TopicId,
                        Name = t.Topic.Name
                    }
                }).ToList(),
                ContestProblems = p.ContestProblems.Select(c => new DbContestProblem
                {
                    Num = c.Num,
                    Contest = new DbContest
                    {
                        Title = c.Contest.Title,
                        EndTime = c.Contest.EndTime
                    }
                }).ToList()
            }).FirstOrDefaultAsync();
        return _mapper.Map<Problem>(problem);
    }

    public async Task<Problem> CreateProblemAsync(Problem problem, int siteId)
    {
        DbProblem dbProblem = _mapper.Map<DbProblem>(problem);
        _context.Problems.Add(dbProblem);
        _context.SaveChanges();

        DbProblem lastProblem = await GetLastInsert();

        _context.ProblemSites.Add(new DbProblemSite {
            problemId = lastProblem.ProblemId.Value,
            SiteId = siteId
        });
        _context.SaveChanges();
        return _mapper.Map<Problem>(lastProblem);
    }

    private async Task<DbProblem> GetLastInsert()
    {
        var lastProblemWithDetails = await _context.Problems
                                        .Include(p => p.ContestProblems)
                                        .Include(p => p.Solutions)
                                        .Include(p => p.Classifications)
                                        .OrderByDescending(p => p.ProblemId)
                                        .FirstOrDefaultAsync();
        return lastProblemWithDetails;
    }

    public async Task<Problem> UpdateProblemAsync(string userId, int problemId, Problem problemToUpdate)
    {
        DbUpdateProblem dbProblem = _mapper.Map<DbUpdateProblem>(problemToUpdate);
        var existingProblem = _context.Problems.FirstOrDefault(p => p.ProblemId == problemId);
        if (existingProblem != null)
        {
            _context.Entry(existingProblem).CurrentValues.SetValues(dbProblem);
            _context.SaveChanges();
            return await GetProblemByIdAsync(problemId);
        }
        else
        {
            throw new KeyNotFoundException("Problem ID not found.");
        }
    }

    public async Task DeleteProblemAsync(int problemId, int siteId)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM problem_site WHERE problem_id = @problemId AND site_id = @siteId",
            new MySqlConnector.MySqlParameter("@problemId", problemId),
            new MySqlConnector.MySqlParameter("@siteId", siteId)
        );
    }
}
