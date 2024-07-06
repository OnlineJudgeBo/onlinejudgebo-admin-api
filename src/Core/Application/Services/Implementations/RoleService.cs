using FluentValidation;

using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;
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

    public async Task<IEnumerable<User>> GetUserRolesAsync()
    {
        return await _roleRepository.GetUserRolesAsync();
    }

    public async Task<IEnumerable<Role>> GetAllRolesAsync()
    {
        return await _roleRepository.GetAllRolesAsync();
    }

    public async Task AddRoleToUserAsync(string userId, int roleId)
    {
        await _roleRepository.AddRoleToUserAsync(userId, roleId);
    }

    public async Task RemoveRoleFromUserAsync(string userId, int roleId)
    {
        await _roleRepository.RemoveRoleFromUserAsync(userId, roleId);
    }
}
