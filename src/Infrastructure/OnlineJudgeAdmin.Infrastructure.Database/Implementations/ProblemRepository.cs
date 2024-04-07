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

    public async Task<IEnumerable<Problem>> GetAllProblemsAsync()
    {
        IEnumerable<DbProblem> problems = await _context.Problems
            .OrderBy(p => p.ProblemId)
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
                Hint =p.Hint,
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

    public async Task<Problem> CreateProblemAsync(Problem problem)
    {
        DbProblem dbProblem = _mapper.Map<DbProblem>(problem);
        _context.Problems.Add(dbProblem);
        _context.SaveChanges();
        Problem createdProblem = _mapper.Map<Problem>(dbProblem);
        return createdProblem;
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
    public Task<Problem> DeleteProblemAsync(int problemId)
    {
        throw new NotImplementedException();
    }

}
