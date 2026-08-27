using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public RoleRepository(AppDbContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<IEnumerable<User>> GetUserRolesAsync(int siteId)
    {
        IEnumerable<DbUser> rolesWithUsers = await _context.Users
            .Where(u => u.SiteId == siteId)
            .Where(u => u.UserRoles.Count != 0)
            .Select(u => new DbUser
            {
                UserId = u.UserId,
                UserProfile = new DbUserProfile
                {
                    Email = u.UserProfile.Email,
                    Nick = u.UserProfile.Nick,
                    Lastname = u.UserProfile.Lastname
                },
                UserRoles = u.UserRoles
                .Where(ur => ur.Role.RoleId != 1)
                .Select(ur => new DbUserRole
                {
                    RoleId = ur.RoleId,
                    Role = new DbRole
                    {
                        RoleId = ur.RoleId,
                        RoleName = ur.Role.RoleName
                    }
                }).ToList()
            }).ToListAsync();

        return _mapper.Map<IEnumerable<User>>(rolesWithUsers);
    }

    public async Task<IEnumerable<Role>> GetAllRolesAsync()
    {
        IEnumerable<DbRole> roles = await _context.Roles
        .Select(t => new DbRole
        {
            RoleId = t.RoleId,
            RoleName = t.RoleName,
        }).ToListAsync();
        return _mapper.Map<IEnumerable<Role>>(roles);
    }

    public async Task<UserRole> GetUserRoleAsync(string userId, int siteId)
    {
        DbUserRole role = await _context.UserRoles
        .Where(u => u.UserId == userId && u.SiteId == siteId)
        .FirstOrDefaultAsync();

        return _mapper.Map<UserRole>(role);
    }

    public async Task AddRoleToUserAsync(string userId, int roleId, int siteId)
    {
        var userRole = new DbUserRole
        {
            UserId = userId,
            RoleId = roleId,
            SiteId = siteId
        };

        _context.UserRoles.Add(userRole);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveRoleFromUserAsync(string userId, int roleId, int siteId)
    {
        var userRole = await _context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId && ur.SiteId == siteId);

        if (userRole is null)
        {
            return;
        }

        _context.UserRoles.Remove(userRole);
        await _context.SaveChangesAsync();
    }
}
