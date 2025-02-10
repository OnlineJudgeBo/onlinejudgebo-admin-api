using FluentValidation;

using ScheduleManager.Core.Domain.Abstractions.Repositories;
using ScheduleManager.Core.Domain.Abstractions.Services;
using ScheduleManager.Core.Domain.Models;

namespace ScheduleManager.Core.Application.Services.Implementations;
public class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IValidator<Problem> _userValidation;

    public RoleService(
        IRoleRepository roleRepository,
        IValidator<Problem> userValidator)
    {
        _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
        _userValidation = userValidator ?? throw new ArgumentNullException(nameof(userValidator));
    }

    public async Task<IEnumerable<User>> GetUserRolesAsync(int siteId)
    {
        return await _roleRepository.GetUserRolesAsync(siteId);
    }

    public async Task<IEnumerable<Role>> GetAllRolesAsync()
    {
        return await _roleRepository.GetAllRolesAsync();
    }

    public async Task AddRoleToUserAsync(string userId, int roleId, int siteId)
    {
        await _roleRepository.AddRoleToUserAsync(userId, roleId, siteId);
    }

    public async Task RemoveRoleFromUserAsync(string userId, int roleId, int siteId)
    {
        await _roleRepository.RemoveRoleFromUserAsync(userId, roleId, siteId);
    }
}
