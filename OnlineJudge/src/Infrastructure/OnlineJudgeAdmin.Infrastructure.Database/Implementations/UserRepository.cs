using System.Data.Common;
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

    // Every column besides users.user_id that stores a user id. news has no ON UPDATE CASCADE and several
    // tables have no FK at all, so each one is updated explicitly. Keep in sync with
    // patito-client-web LoginRepository::renameUser.
    private static readonly (string Table, string Column)[] AppTablesWithUserId =
    {
        ("user_profiles", "user_id"), ("user_roles", "user_id"), ("user_settings", "user_id"),
        ("user_activity", "user_id"), ("solution", "user_id"), ("contest_user", "user_id"),
        ("privilege", "user_id"), ("news", "user_id"), ("custom_input", "user_id"),
        ("loginlog", "user_id"), ("online_history", "user_id"),
    };

    private static readonly (string Table, string Column)[] AcademicTablesWithUserId =
    {
        ("course", "created_by_user_id"), ("course_user", "user_id"), ("course_assignment", "created_by_user_id"),
        ("course_content_item", "created_by_user_id"), ("course_submission_context", "user_id"),
        ("learning_path_progress", "user_id"), ("learning_path_topic_progress", "user_id"),
    };

    public async Task<User> UpdateUser(User userToUpdate, string userId, int siteId)
    {
        string newUserId = userToUpdate.UserId;
        bool isMySql = _context.Database.IsMySql();
        var appConnection = _context.Database.GetDbConnection();
        var academicConnection = _academicContext.Database.GetDbConnection();
        // Default setup: the academic schema lives on the same server, so it joins the same transaction via
        // db-qualified names. If AcademicConnection points elsewhere it's renamed after commit (best effort).
        string? academicSchema = isMySql && appConnection.DataSource == academicConnection.DataSource
            ? academicConnection.Database
            : null;

        await using var transaction = await _context.Database.BeginTransactionAsync();
        if (isMySql)
            await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0");
        try
        {
            int renamed = await _context.Database.ExecuteSqlRawAsync(
                "UPDATE users SET user_id = {0} WHERE user_id = {1} AND site_id = {2}", newUserId, userId, siteId);
            if (renamed == 0)
                throw new KeyNotFoundException($"El usuario {userId} no existe en este sitio.");

            foreach (var (table, column) in AppTablesWithUserId)
                await RenameColumnAsync(_context, table, column, newUserId, userId);

            if (academicSchema != null)
                foreach (var (table, column) in AcademicTablesWithUserId)
                    await RenameColumnAsync(_context, $"`{academicSchema}`.{table}", column, newUserId, userId);

            await transaction.CommitAsync();
        }
        finally
        {
            if (isMySql)
                await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1");
        }

        if (academicSchema == null)
            foreach (var (table, column) in AcademicTablesWithUserId)
                await RenameColumnAsync(_academicContext, table, column, newUserId, userId);

        return _mapper.Map<User>(userToUpdate);
    }

    private static async Task RenameColumnAsync(DbContext context, string table, string column, string newUserId, string oldUserId)
    {
        try
        {
            // table/column only come from the fixed arrays above, never from user input.
#pragma warning disable EF1002
            await context.Database.ExecuteSqlRawAsync($"UPDATE {table} SET {column} = {{0}} WHERE {column} = {{1}}", newUserId, oldUserId);
#pragma warning restore EF1002
        }
        catch (DbException ex) when (IsMissingTable(ex))
        {
            // Deployments without the academic schema (or tables not in this context): nothing to rename there.
        }
    }

    private static bool IsMissingTable(DbException ex) =>
        ex is MySqlConnector.MySqlException { ErrorCode: MySqlConnector.MySqlErrorCode.NoSuchTable or MySqlConnector.MySqlErrorCode.UnknownDatabase }
        || ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase);

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
