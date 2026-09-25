using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

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
            .Where(u => u.IsActive
                && !u.IsDeleted
                && u.SiteId == siteId
                && u.UserProfile.SiteId == siteId)
            .Take(100)
            .Select(u => new DbUser
            {
                UserId = u.UserId,
                IsActive = u.IsActive,
                IsDeleted = u.IsDeleted,
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
        // users.user_id is UNIQUE across sites, and not every user has a user_settings row.
        return !await _context.Users.AnyAsync(u => u.UserId == userProfile.UserId);
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
            .Where(u => u.IsActive
                && !u.IsDeleted
                && u.SiteId == siteId
                && u.UserProfile.SiteId == siteId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(u =>
                u.UserProfile.Nick.Contains(searchTerm) ||
                u.UserProfile.Lastname.Contains(searchTerm) ||
                u.UserProfile.Email.Contains(searchTerm) ||
                u.UserId.Contains(searchTerm));
        }

        IEnumerable<DbUser> users = await query
            .Select(u => new DbUser
            {
                UserId = u.UserId,
                IsActive = u.IsActive,
                IsDeleted = u.IsDeleted,
                UserProfile = new DbUserProfile
                {
                    Email = u.UserProfile.Email,
                    Nick = u.UserProfile.Nick,
                    Lastname = u.UserProfile.Lastname
                }
            }).ToListAsync();

        return _mapper.Map<IEnumerable<User>>(users);
    }


    public async Task<bool> UserIdExists(string userId, string exceptUserId)
    {
        // users.user_id is UNIQUE across sites. The except clause lets a case-only rename (Juan -> juan) pass the ci collation.
        return await _context.Users.AnyAsync(u => u.UserId == userId && u.UserId != exceptUserId);
    }

    // Every column that stores a user_id. news has no ON UPDATE CASCADE and several tables have no FK at all,
    // so FK checks are disabled and each table is updated explicitly. Keep in sync with
    // patito-client-web LoginRepository::renameUser.
    private static readonly string[] RenameUserStatements =
    {
        "UPDATE user_profiles SET user_id = @NewUserId WHERE user_id = @UserId",
        "UPDATE user_roles SET user_id = @NewUserId WHERE user_id = @UserId",
        "UPDATE user_settings SET user_id = @NewUserId WHERE user_id = @UserId",
        "UPDATE user_activity SET user_id = @NewUserId WHERE user_id = @UserId",
        "UPDATE solution SET user_id = @NewUserId WHERE user_id = @UserId",
        "UPDATE contest_user SET user_id = @NewUserId WHERE user_id = @UserId",
        "UPDATE privilege SET user_id = @NewUserId WHERE user_id = @UserId",
        "UPDATE news SET user_id = @NewUserId WHERE user_id = @UserId",
        "UPDATE custom_input SET user_id = @NewUserId WHERE user_id = @UserId",
        "UPDATE loginlog SET user_id = @NewUserId WHERE user_id = @UserId",
        "UPDATE online_history SET user_id = @NewUserId WHERE user_id = @UserId",
        "UPDATE academic.course SET created_by_user_id = @NewUserId WHERE created_by_user_id = @UserId",
        "UPDATE academic.course_user SET user_id = @NewUserId WHERE user_id = @UserId",
        "UPDATE academic.course_assignment SET created_by_user_id = @NewUserId WHERE created_by_user_id = @UserId",
        "UPDATE academic.course_content_item SET created_by_user_id = @NewUserId WHERE created_by_user_id = @UserId",
        "UPDATE academic.course_submission_context SET user_id = @NewUserId WHERE user_id = @UserId",
        "UPDATE academic.learning_path_progress SET user_id = @NewUserId WHERE user_id = @UserId",
        "UPDATE academic.learning_path_topic_progress SET user_id = @NewUserId WHERE user_id = @UserId",
    };

    public async Task<User> UpdateUser(User userToUpdate, string userId, int siteId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0");
        try
        {
            int renamed = await _context.Database.ExecuteSqlRawAsync(
                "UPDATE users SET user_id = @NewUserId WHERE user_id = @UserId AND site_id = @SiteId",
                new MySqlConnector.MySqlParameter("@NewUserId", userToUpdate.UserId),
                new MySqlConnector.MySqlParameter("@UserId", userId),
                new MySqlConnector.MySqlParameter("@SiteId", siteId));
            if (renamed == 0)
                throw new KeyNotFoundException($"El usuario {userId} no existe en este sitio.");

            foreach (string statement in RenameUserStatements)
            {
                try
                {
                    await _context.Database.ExecuteSqlRawAsync(statement,
                        new MySqlConnector.MySqlParameter("@NewUserId", userToUpdate.UserId),
                        new MySqlConnector.MySqlParameter("@UserId", userId));
                }
                catch (MySqlConnector.MySqlException ex) when (ex.ErrorCode == MySqlConnector.MySqlErrorCode.NoSuchTable
                                                                || ex.ErrorCode == MySqlConnector.MySqlErrorCode.UnknownDatabase)
                {
                    // Deployments without the academic schema: nothing to rename there.
                }
            }
            await transaction.CommitAsync();
        }
        finally
        {
            await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1");
        }

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
