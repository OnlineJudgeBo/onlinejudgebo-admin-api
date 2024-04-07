using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public class TopicRepository : ITopicRepository
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public TopicRepository(AppDbContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<IEnumerable<Topic>> GetAllTopicsAsync()
    {
        var topics = await _context.Topics
        .Select(t => new DbTopic
        {
            TopicId = t.TopicId,
            Name = t.Name,
            Classifications = t.Classifications.Select(t => new DbClassification
            {
                Name = t.Name,
                ClassificationId = t.ClassificationId
            }).ToList(),
        }).ToListAsync();
        return _mapper.Map<IEnumerable<Topic>>(topics);
    }

    public async Task AddClassificationsToProblemAsync(int problem_id, IEnumerable<Classification> Classifications)
    {
        var problem = await _context
            .Problems.Include(p => p.Classifications)
            .FirstOrDefaultAsync(p => p.ProblemId == problem_id);

        foreach (Classification classification in Classifications)
        {
            var classificationDb = await _context.Classifications
                .FirstOrDefaultAsync(c => c.ClassificationId == classification.ClassificationId);
            problem.Classifications.Add(classificationDb);
        }
        await _context.SaveChangesAsync();
    }

    public async Task RemoveAllClassificationsFromProblemAsync(int problemId)
    {
        var problem = await _context.Problems
            .Include(p => p.Classifications)
            .FirstOrDefaultAsync(p => p.ProblemId == problemId);

        if (problem != null)
        {
            var classificationsToRemove = problem.Classifications
                .ToList();

            foreach (var classification in classificationsToRemove)
            {
                problem.Classifications.Remove(classification);
            }

            await _context.SaveChangesAsync();
        }
    }

}
