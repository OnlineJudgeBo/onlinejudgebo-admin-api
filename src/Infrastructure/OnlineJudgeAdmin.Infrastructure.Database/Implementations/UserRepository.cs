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
        .Take(100)
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

    public async Task<User> GetUserById(string userId)
    {
        try
        {
            DbUser users = await _context.Users
                .Where(u => u.UserId == userId)
                .Select(t => new DbUser
                {
                    UserId = t.UserId
                }).FirstAsync();
            return _mapper.Map<User>(users);
        }
        catch (Exception e)
        {
            return null;
        }
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

    public async Task<User> UpdateUser(User userToUpdate, string userId)
    {
        string sqlQuery = @"
            UPDATE users
            SET user_id = @NewUserId
            WHERE user_id = @UserId;
            ";

        await _context.Database.ExecuteSqlRawAsync(sqlQuery,
            new MySqlConnector.MySqlParameter("@NewUserId", userToUpdate.UserId),
            new MySqlConnector.MySqlParameter("@UserId", userId)
        );

        return _mapper.Map<User>(userToUpdate);
    }

    public async Task<UserProfile> UpdateUserProfile(UserProfile profileToUpdate)
    {
        DbUserProfile userProfile = await _context.UserProfiles
                .FirstOrDefaultAsync(u => u.UserId == profileToUpdate.UserId);

        if (userProfile != null)
        {
            userProfile.Nick = profileToUpdate.Nick;
            userProfile.Lastname = profileToUpdate.Lastname;
            userProfile.Email = profileToUpdate.Email;
            await _context.SaveChangesAsync();
        }

        return _mapper.Map<UserProfile>(userProfile);
    }

    public async Task ChangePassword(string newPasswordEncode, string userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);

        if (user != null)
        {
            user.Password = newPasswordEncode.Substring(0, 32);

            _context.Entry(user).Property(u => u.Password).IsModified = true;

            await _context.SaveChangesAsync();
        }
        else
        {
            throw new Exception("User not found");
        }
    }

    public async Task DeleteRoleAsync(string userId, int roleId)
    {
        var roles = _context.UserRoles.Where(ur => ur.UserId == userId && ur.RoleId == roleId).ToList();

        if (roles.Any())
        {
            _context.UserRoles.RemoveRange(roles);
            await _context.SaveChangesAsync();
        }
    }
}
