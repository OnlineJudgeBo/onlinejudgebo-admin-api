using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Models;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;
    private readonly AcademicCatalogDbContext _academicContext;
    private readonly IMapper _mapper;

    public UserRepository(AppDbContext context, AcademicCatalogDbContext academicContext, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _academicContext = academicContext ?? throw new ArgumentNullException(nameof(academicContext));
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
        DbUser? user = await _context.Users
            .Where(u => u.SiteId == siteId && u.UserId == userId)
            .Select(u => new DbUser
            {
                UserId = u.UserId
            }).FirstOrDefaultAsync();
        return _mapper.Map<User>(user);
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


    // user_id is the PK of `users`, but it's also duplicated - with no
    // DB-level FK cascade - as a plain column on every table below. Renaming
    // it must fan out to all of them, or those rows are silently left
    // pointing at a user_id that no longer exists (profile, roles,
    // submissions, privileges... effectively orphaned).
    private static readonly string[] AppDbTablesWithUserId =
    {
        "privilege", "custom_input", "user_profiles", "contest_user", "solution",
        "user_settings", "user_activity", "news", "loginlog", "user_roles"
    };

    private static readonly string[] AcademicTablesWithUserId =
    {
        "course_user", "learning_path_topic_progress", "learning_path_progress", "course_submission_context"
    };

    public async Task<User> UpdateUser(User userToUpdate, string userId, int siteId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        // {0}/{1}/... positional placeholders (not string-concatenated
        // values) - EF builds a provider-correct parameter for each, so
        // this isn't tied to MySqlConnector specifically and isn't SQL
        // injectable.
        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE users SET user_id = {0} WHERE user_id = {1} AND site_id = {2};",
            userToUpdate.UserId, userId, siteId);

        // EF1002 fires on any interpolated ExecuteSqlRawAsync string, but
        // `table` is never user input - it only ever comes from the fixed
        // arrays declared above, so this is not an injection risk.
#pragma warning disable EF1002
        foreach (string table in AppDbTablesWithUserId)
        {
            await _context.Database.ExecuteSqlRawAsync($"UPDATE {table} SET user_id = {{0}} WHERE user_id = {{1}};", userToUpdate.UserId, userId);
        }

        await transaction.CommitAsync();

        // AcademicCatalogDbContext uses its own connection, so this half
        // can't share the transaction above - best-effort, not atomic with
        // it. Still strictly better than leaving these four tables
        // uncascaded entirely.
        foreach (string table in AcademicTablesWithUserId)
        {
            await _academicContext.Database.ExecuteSqlRawAsync($"UPDATE {table} SET user_id = {{0}} WHERE user_id = {{1}};", userToUpdate.UserId, userId);
        }
#pragma warning restore EF1002

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
