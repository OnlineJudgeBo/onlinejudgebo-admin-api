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
            .Where(c => c.ProblemSites.Any(site => site.SiteId == siteId && site.IsActive))
            .Where(p => p.Defunct == "N" || p.Defunct == "Y")
            .OrderByDescending(p => p.ProblemId)
            .Select(p => new DbProblem
            {
                ProblemId = p.ProblemId,
                Title = p.Title,
                Source = p.Source,
                OriginSource = p.OriginSource,
                InDate = p.InDate,
                Submit = p.Submit,
                Accepted = p.Accepted,
                Defunct = p.Defunct,
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
            .Where(c => c.ProblemSites.Any(site => site.SiteId == siteId && site.IsActive))
            .OrderByDescending(p => p.ProblemId)
            .Select(p => new DbProblem
            {
                ProblemId = p.ProblemId,
                Title = p.Title,
                Source = p.Source,
                OriginSource = p.OriginSource,
                InDate = p.InDate,
                Submit = p.Submit,
                Accepted = p.Accepted,
                Defunct = p.Defunct,
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

    public async Task<IEnumerable<Problem>> SearchProblemForAdminAsync(string searchTerm, int siteId)
    {
        IQueryable<DbProblem> query = _context.Problems
            .Where(p => p.ProblemSites.Any(site => site.SiteId == siteId && site.IsActive))
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
                OriginSource = po.OriginSource,
                Defunct = po.Defunct,
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

    public async Task<IEnumerable<Problem>> SearchProblemAsync(string searchTerm, int siteId)
    {
        IQueryable<DbProblem> query = _context.Problems
            .Where(p => p.ProblemSites.Any(site => site.SiteId == siteId && site.IsActive))
            .Where(p => p.Defunct == "N" || p.Defunct == "Y")
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
                OriginSource = po.OriginSource,
                Defunct = po.Defunct,
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

    public async Task<Problem> GetProblemByIdAsync(int problemId, int? siteId = null)
    {
        IQueryable<DbProblem> query = _context.Problems
            .Where(p => p.ProblemId == problemId);

        if (siteId.HasValue)
        {
            query = query.Where(p => p.ProblemSites.Any(site => site.SiteId == siteId.Value && site.IsActive));
        }

        DbProblem? problem = await query
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
                OriginSource = p.OriginSource,
                Defunct = p.Defunct,
                Spj = p.Spj,
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

        _context.ProblemSites.Add(new DbProblemSite
        {
            problemId = lastProblem.ProblemId.Value,
            SiteId = siteId,
            IsActive = true
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
        var existingProblem = _context.Problems.FirstOrDefault(p => p.ProblemId == problemId);
        if (existingProblem != null)
        {
            if (string.IsNullOrWhiteSpace(problemToUpdate.Defunct))
            {
                problemToUpdate.Defunct = existingProblem.Defunct;
            }

            DbUpdateProblem dbProblem = _mapper.Map<DbUpdateProblem>(problemToUpdate);
            _context.Entry(existingProblem).CurrentValues.SetValues(dbProblem);
            _context.SaveChanges();
            return await GetProblemByIdAsync(problemId);
        }
        else
        {
            throw new KeyNotFoundException("Problem ID not found.");
        }
    }

    public async Task PromoteProblemAsync(List<int> problemIds)
    {
        var existingProblems = await _context.Problems
            .Where(p => problemIds.Contains(p.ProblemId ?? 0))
            .ToListAsync();

        if (existingProblems == null || existingProblems.Count == 0)
            throw new KeyNotFoundException("No problems found with the given IDs.");

        foreach (var problem in existingProblems)
        {
            problem.Defunct = "O";
        }

        await _context.SaveChangesAsync();
    }

    public async Task ChangeProblemVisibilityAsync(int problemId)
    {
        var existingProblem = _context.Problems
            .FirstOrDefault(p => p.ProblemId == problemId);

        if (existingProblem == null)
            throw new KeyNotFoundException("No problems found with the given IDs.");

        if (existingProblem.Defunct == "Y")
        {
            existingProblem.Defunct = "N";
        }
        else if (existingProblem.Defunct != "O")
        {
            existingProblem.Defunct = "Y";
        }
        await _context.SaveChangesAsync();
    }

    public async Task DeleteProblemAsync(int problemId, int siteId)
    {
        var problemSite = await _context.ProblemSites
            .FirstOrDefaultAsync(item => item.problemId == problemId && item.SiteId == siteId);

        if (problemSite == null)
        {
            return;
        }

        problemSite.IsActive = false;
        await _context.SaveChangesAsync();
    }
}
