using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public UserRepository(AppDbContext context, IMapper mapper)
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
        var problem = await _context.Classifications.FindAsync(problem_id);

        foreach (Classification classification in Classifications)
        {
            var topic1 = await _context.Classifications.FindAsync(classification.TopicId);

            _context.Classifications.Add(topic1);
        }
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<User>> GetAllUsersProfilesAsync()
    {
        IEnumerable<DbUser> users = await _context.Users
        .OrderBy(p => p.UserId)
        .Where(u => u.IsActive)
        .Select(t => new DbUser
        {
            UserId = t.UserId,
            UserProfile = new DbUserProfile
            {
                Email = t.UserProfile.Email,
                Nick = t.UserProfile.Nick,
                Lastname = t.UserProfile.Lastname
            }
        }).ToListAsync();
        return _mapper.Map<IEnumerable<User>>(users);
    }

    public async Task<bool> CheckUsernameAvailable(UserProfile userProfile)
    {
        return !await _context.UserSettings.AnyAsync(u => u.UserId == userProfile.UserId);
    }

    public async Task<bool> CheckUserEmailAvailable(UserProfile userProfile)
    {
        return !await _context.UserProfiles.AnyAsync(u => u.Email == userProfile.Email);
    }

    public async Task<IEnumerable<User>> SearchUserProfilesAsync(string? searchTerm = null)
    {
        IQueryable<DbUser> query = _context.Users
            .OrderBy(p => p.UserId)
            .Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(u =>
                u.UserProfile.Nick.Contains(searchTerm) ||
                u.UserProfile.Lastname.Contains(searchTerm) ||
                u.UserId.Contains(searchTerm));
        }

        IEnumerable<DbUser> users = await query
            .Select(t => new DbUser
            {
                UserId = t.UserId,
                UserProfile = new DbUserProfile
                {
                    Email = t.UserProfile.Email,
                    Nick = t.UserProfile.Nick,
                    Lastname = t.UserProfile.Lastname
                }
            }).ToListAsync();

        return _mapper.Map<IEnumerable<User>>(users);
    }
}
