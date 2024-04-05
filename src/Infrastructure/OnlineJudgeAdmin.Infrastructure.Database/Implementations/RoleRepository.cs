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

    public async Task<IEnumerable<User>> GetUserRolesAsync()
    {
        IEnumerable<DbUser> rolesWithUsers = await _context.Users
            .Where(u => u.Roles.Any())
            .Select(u => new DbUser
            {
                UserId = u.UserId,
                UserProfile = new DbUserProfile
                {
                    Email = u.UserProfile.Email,
                    Nick = u.UserProfile.Nick,
                    Lastname = u.UserProfile.Lastname
                },
                Roles = u.Roles.Select(ur => new DbRole
                {
                    RoleId = ur.RoleId,
                    RoleName = ur.RoleName
                }).ToList()
            }).ToListAsync();

        return _mapper.Map<IEnumerable<User>>(rolesWithUsers);
    }

    public async Task<IEnumerable<Role>> GetNameRolesAsync()
    {
        IEnumerable<DbRole> roles = await _context.Roles
        .Select(t => new DbRole
        {
            RoleId = t.RoleId,
            RoleName = t.RoleName
        }).ToListAsync();
        return _mapper.Map<IEnumerable<Role>>(roles);
    }
}
