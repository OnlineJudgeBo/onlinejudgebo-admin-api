using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ScheduleManager.Core.Domain.Abstractions.Repositories;
using ScheduleManager.Core.Domain.Models;
using ScheduleManager.Infrastructure.Database.Models;

namespace ScheduleManager.Infrastructure.Database.Implementations;

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
            Classifications = t.Classifications.Select(c => new DbClassification
            {
                Name = c.Name,
                ClassificationId = c.ClassificationId
            }).ToList(),
        }).ToListAsync();
        return _mapper.Map<IEnumerable<Topic>>(topics);
    }

    public async Task AddClassificationsToProblemAsync(int problemId, IEnumerable<Classification> classifications)
    {
        var problem = await _context.Classifications.FindAsync(problemId);

        foreach (var classification in classifications)
        {
            var topic = await _context.Classifications.FindAsync(classification.TopicId);

            _context.Classifications.Add(topic);
        }
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<User>> GetAllUsersProfilesAsync(int siteId)
    {
        IEnumerable<DbUser> users = await _context.Users
            .OrderBy(u => u.UserId)
            .Where(u => u.IsActive && u.SiteId == siteId && u.UserProfile.SiteId == siteId)
            .Take(100)
            .Select(u => new DbUser
            {
                UserId = u.UserId,
                UserProfile = new DbUserProfile
                {
                    Email = u.UserProfile.Email,
                    Nick = u.UserProfile.Nick,
                    Lastname = u.UserProfile.Lastname
                }
            }).ToListAsync();
        return _mapper.Map<IEnumerable<User>>(users);
    }

    public async Task<User> GetUserById(string userId, int siteId)
    {
        try
        {
            DbUser user = await _context.Users
                .Where(u => u.SiteId == siteId && u.UserId == userId)
                .Select(u => new DbUser
                {
                    UserId = u.UserId
                }).FirstAsync();
            return _mapper.Map<User>(user);
        }
        catch (Exception e)
        {
            return null;
        }
    }

    public async Task<bool> CheckUsernameAvailable(UserProfile userProfile, int siteId)
    {
        return !await _context.UserSettings
            .AnyAsync(u => u.UserId == userProfile.UserId && u.SiteId == siteId);
    }

    public async Task<bool> CheckUserEmailAvailable(UserProfile userProfile, int siteId)
    {
        return !await _context.UserProfiles
            .AnyAsync(u => u.Email == userProfile.Email && u.SiteId == siteId);
    }

    public async Task<IEnumerable<User>> SearchUserProfilesAsync(string? searchTerm, int siteId)
    {
        IQueryable<DbUser> query = _context.Users
            .OrderBy(u => u.UserId)
            .Where(u => u.IsActive && u.SiteId == siteId && u.UserProfile.SiteId == siteId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(u =>
                u.UserProfile.Nick.Contains(searchTerm) ||
                u.UserProfile.Lastname.Contains(searchTerm) ||
                u.UserId.Contains(searchTerm));
        }

        IEnumerable<DbUser> users = await query
            .Select(u => new DbUser
            {
                UserId = u.UserId,
                UserProfile = new DbUserProfile
                {
                    Email = u.UserProfile.Email,
                    Nick = u.UserProfile.Nick,
                    Lastname = u.UserProfile.Lastname
                }
            }).ToListAsync();

        return _mapper.Map<IEnumerable<User>>(users);
    }


    public async Task<User> UpdateUser(User userToUpdate, string userId, int siteId)
    {
        string sqlQuery = @"
            UPDATE users
            SET user_id = @NewUserId
            WHERE user_id = @UserId AND site_id = @SiteId;
            ";

        await _context.Database.ExecuteSqlRawAsync(sqlQuery,
            new MySqlConnector.MySqlParameter("@NewUserId", userToUpdate.UserId),
            new MySqlConnector.MySqlParameter("@UserId", userId),
            new MySqlConnector.MySqlParameter("@SiteId", siteId)
        );

        return _mapper.Map<User>(userToUpdate);
    }

    public async Task<UserProfile> UpdateUserProfile(UserProfile profileToUpdate, int siteId)
    {
        DbUserProfile userProfile = await _context.UserProfiles
            .FirstOrDefaultAsync(u => u.UserId == profileToUpdate.UserId && u.SiteId == siteId);

        if (userProfile != null)
        {
            userProfile.Nick = profileToUpdate.Nick;
            userProfile.Lastname = profileToUpdate.Lastname;
            userProfile.Email = profileToUpdate.Email;
            await _context.SaveChangesAsync();
        }

        return _mapper.Map<UserProfile>(userProfile);
    }

    public async Task ChangePassword(string newPasswordEncode, string userId, int siteId)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == userId && u.SiteId == siteId);

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

    public async Task DeleteRoleAsync(string userId, int roleId, int siteId)
    {
        var roles = _context.UserRoles
            .Where(ur => ur.UserId == userId && ur.RoleId == roleId && ur.SiteId == siteId)
            .ToList();

        if (roles.Count != 0)
        {
            _context.UserRoles.RemoveRange(roles);
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteUserAsync(string userId, int siteId)
    {
        DbUser user = await _context.Users
            .Where(u => u.UserId == userId && u.SiteId == siteId)
            .FirstOrDefaultAsync();

        if (user != null)
        {
            user.IsActive = false;
            _context.Entry(user).Property(c => c.IsActive).IsModified = true;
            await _context.SaveChangesAsync();
        }
    }
}
