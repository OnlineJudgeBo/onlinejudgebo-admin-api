using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ScheduleManager.Core.Domain.Abstractions.Repositories;
using ScheduleManager.Core.Domain.Models;
using ScheduleManager.Infrastructure.Database.Models;

namespace ScheduleManager.Infrastructure.Database.Implementations;

public class TopicRepository : ITopicRepository
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public TopicRepository(AppDbContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Classification> GetClassificationById(int classificationId)
    {
        DbClassification dbClassification = await _context.Classifications
            .FirstOrDefaultAsync(classification => classification.ClassificationId == classificationId);

        return _mapper.Map<Classification>(dbClassification);
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

        if (problem == null) throw new ArgumentException("Problem not found.");

        var classificationsToRemove = problem.Classifications
            .ToList();

        foreach (var classification in classificationsToRemove)
        {
            problem.Classifications.Remove(classification);
        }

        await _context.SaveChangesAsync();

    }

    public async Task CreateTopic(Topic newTopic)
    {
        DbTopic topic = new DbTopic
        {
            Name = newTopic.Name,
        };

        await _context.Topics.AddAsync(topic);
        await _context.SaveChangesAsync();
    }

    public async Task AddClassificationToTopic(int id, Topic classificationTopic)
    {
        DbTopic topic = _mapper.Map<DbTopic>(classificationTopic);
        var existingTopic = await _context.Topics.FirstOrDefaultAsync(t => t.TopicId == id);
        foreach (DbClassification classification in topic.Classifications)
        {
            existingTopic.Classifications.Add(classification);
        }

        await _context.SaveChangesAsync();
    }

    public async Task UpdateClassification(Classification classificationToUpdate, int classificationId)
    {
        DbClassification classification = _mapper.Map<DbClassification>(classificationToUpdate);
        var existingClassification = _context.Classifications
            .FirstOrDefault(c => c.ClassificationId == classificationId);

        if (existingClassification == null) throw new ArgumentException("Classification not found.");

        existingClassification.Name = classificationToUpdate.Name;
        _context.Entry(existingClassification).Property(c => c.Name).IsModified = true;
        _context.SaveChanges();
    }
}
