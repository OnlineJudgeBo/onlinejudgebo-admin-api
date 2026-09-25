using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public partial class PublicRepository
{
    public async Task<PublicAuthenticatedUser> LoginAsync(string userOrEmail, string password, int siteId)
    {
        var normalizedUserOrEmail = userOrEmail.Trim();
        var normalizedEmail = normalizedUserOrEmail.ToLowerInvariant();

        var user = await (
            from item in _context.Users
            join profile in _context.UserProfiles.Where(profile => profile.SiteId == siteId)
                on item.UserId equals profile.UserId into profileJoin
            from profile in profileJoin.DefaultIfEmpty()
            where item.SiteId == siteId
                && !item.IsDeleted
                && item.IsActive
                && (item.UserId == normalizedUserOrEmail
                    || (!string.IsNullOrWhiteSpace(profile.Email) && profile.Email!.ToLower() == normalizedEmail))
            select new
            {
                item.UserId,
                item.Password,
                Nick = profile != null ? profile.Nick : item.UserId,
                LastName = profile != null ? profile.Lastname : string.Empty,
                Email = profile != null ? profile.Email : string.Empty
            }
        ).FirstOrDefaultAsync();

        if (user == null || !LegacyPasswordHasher.Verify(password, user.Password))
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        await _context.Users
            .Where(item => item.UserId == user.UserId && item.SiteId == siteId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Accesstime, DateTime.Now));

        return new PublicAuthenticatedUser
        {
            UserId = user.UserId,
            Nick = string.IsNullOrWhiteSpace(user.Nick) ? user.UserId : user.Nick,
            LastName = user.LastName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Role = await ResolveUserRoleAsync(user.UserId, siteId),
            SiteId = siteId
        };
    }

    public async Task<PublicAuthenticatedUser> RegisterAsync(string userId, string passwordHash, string email, string? nick, string? lastName, string? school, int siteId, string ipAddress)
    {
        var emailLower = email.ToLowerInvariant();

        var userExists = await _context.Users
            .AnyAsync(user => user.UserId == userId);

        if (userExists)
        {
            throw new InvalidOperationException("El usuario ya existe.");
        }

        var emailExists = await _context.UserProfiles
            .AnyAsync(profile => !string.IsNullOrWhiteSpace(profile.Email) && profile.Email!.ToLower() == emailLower);

        if (emailExists)
        {
            throw new InvalidOperationException("El correo electrónico ya está registrado.");
        }

        var now = DateTime.Now;
        var role = nameof(UserRolesEnum.Invitado);

        await using var transaction = await _context.Database.BeginTransactionAsync();

        await _context.Users.AddAsync(new DbUser
        {
            UserId = userId,
            Password = passwordHash,
            Ip = ipAddress,
            Accesstime = now,
            SiteId = siteId,
            RegTime = now,
            IsActive = true,
            IsDeleted = false
        });

        await _context.UserProfiles.AddAsync(new DbUserProfile
        {
            UserId = userId,
            SiteId = siteId,
            Email = email,
            Nick = nick ?? userId,
            School = school,
            Lastname = lastName,
            Departament = 0
        });

        await _context.UserActivities.AddAsync(new DbUserActivity
        {
            UserId = userId,
            SiteId = siteId,
            Submit = 0,
            Solved = 0
        });

        var guestRole = await _context.Roles
            .Where(roleItem => roleItem.RoleName.ToLower() == "invitado" || roleItem.RoleName.ToLower() == "guest")
            .OrderBy(roleItem => roleItem.RoleId)
            .Select(roleItem => new
            {
                roleItem.RoleId,
                roleItem.RoleName
            })
            .FirstOrDefaultAsync();

        if (guestRole != null)
        {
            if (string.IsNullOrWhiteSpace(guestRole.RoleName))
            {
                role = nameof(UserRolesEnum.Invitado);
            }
            else
            {
                var roleName = guestRole.RoleName.Trim().ToLowerInvariant();
                role = roleName.Contains("admin")
                    ? nameof(UserRolesEnum.Administrador)
                    : roleName.Contains("aux")
                        ? nameof(UserRolesEnum.Auxiliar)
                        : roleName.Contains("doc")
                            ? nameof(UserRolesEnum.Docente)
                            : nameof(UserRolesEnum.Invitado);
            }

            await _context.UserRoles.AddAsync(new DbUserRole
            {
                UserId = userId,
                RoleId = guestRole.RoleId,
                SiteId = siteId
            });
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return new PublicAuthenticatedUser
        {
            UserId = userId,
            Nick = nick ?? userId,
            LastName = lastName ?? string.Empty,
            Email = email,
            Role = role,
            SiteId = siteId
        };
    }

    public async Task<PublicPasswordRecoveryTarget?> GetPasswordRecoveryTargetAsync(string email, int siteId)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        return await (
            from item in _context.Users
            join profile in _context.UserProfiles.Where(profile => profile.SiteId == siteId)
                on item.UserId equals profile.UserId
            where item.SiteId == siteId
                && !item.IsDeleted
                && item.IsActive
                && !string.IsNullOrWhiteSpace(profile.Email)
                && profile.Email!.ToLower() == normalizedEmail
            select new PublicPasswordRecoveryTarget
            {
                UserId = item.UserId,
                Email = profile.Email!,
                Nick = string.IsNullOrWhiteSpace(profile.Nick) ? item.UserId : profile.Nick!
            }
        ).FirstOrDefaultAsync();
    }

    public async Task SavePasswordRecoveryTokenAsync(string userId, int siteId, string tokenHash, DateTime expiresAtUtc)
    {
        var user = await _context.Users.FirstOrDefaultAsync(item =>
            item.UserId == userId
            && item.SiteId == siteId
            && !item.IsDeleted
            && item.IsActive);

        if (user == null)
        {
            throw new KeyNotFoundException("Usuario no encontrado.");
        }

        user.ResetPasswordToken = tokenHash;
        user.ResetPasswordExpires = expiresAtUtc;
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ResetPasswordWithTokenAsync(string email, int siteId, string tokenHash, DateTime nowUtc, string passwordHash)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await (
            from item in _context.Users
            join profile in _context.UserProfiles.Where(profile => profile.SiteId == siteId)
                on item.UserId equals profile.UserId
            where item.SiteId == siteId
                && !item.IsDeleted
                && item.IsActive
                && !string.IsNullOrWhiteSpace(profile.Email)
                && profile.Email!.ToLower() == normalizedEmail
                && item.ResetPasswordToken == tokenHash
                && item.ResetPasswordExpires.HasValue
                && item.ResetPasswordExpires.Value >= nowUtc
            select item
        ).FirstOrDefaultAsync();

        if (user == null)
        {
            return false;
        }

        await _context.Users
            .Where(item => item.UserId == user.UserId && item.SiteId == siteId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Password, passwordHash)
                .SetProperty(item => item.ResetPasswordToken, (string?)null)
                .SetProperty(item => item.ResetPasswordExpires, (DateTime?)null)
                .SetProperty(item => item.Accesstime, nowUtc));
        return true;
    }

    public async Task<PublicAuthenticatedUser> GetAuthenticatedUserAsync(string userId, int siteId)
    {
        var user = await (
            from item in _context.Users
            join profile in _context.UserProfiles.Where(profile => profile.SiteId == siteId)
                on item.UserId equals profile.UserId into profileJoin
            from profile in profileJoin.DefaultIfEmpty()
            where item.UserId == userId
                && item.SiteId == siteId
                && !item.IsDeleted
                && item.IsActive
            select new
            {
                item.UserId,
                Nick = profile != null ? profile.Nick : item.UserId,
                LastName = profile != null ? profile.Lastname : string.Empty,
                Email = profile != null ? profile.Email : string.Empty
            }
        ).FirstOrDefaultAsync();

        if (user == null)
        {
            throw new UnauthorizedAccessException("Unauthorized user.");
        }

        return new PublicAuthenticatedUser
        {
            UserId = user.UserId,
            Nick = string.IsNullOrWhiteSpace(user.Nick) ? user.UserId : user.Nick,
            LastName = user.LastName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Role = await ResolveUserRoleAsync(user.UserId, siteId),
            SiteId = siteId
        };
    }

    private async Task<string> ResolveUserRoleAsync(string userId, int siteId)
    {
        var roleName = await (
            from userRole in _context.UserRoles
            join role in _context.Roles on userRole.RoleId equals role.RoleId
            where userRole.UserId == userId && userRole.SiteId == siteId
            orderby role.RoleId
            select role.RoleName
        ).FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(roleName))
        {
            return nameof(UserRolesEnum.Invitado);
        }

        var value = roleName.Trim().ToLowerInvariant();
        if (value.Contains("admin"))
        {
            return nameof(UserRolesEnum.Administrador);
        }

        if (value.Contains("aux"))
        {
            return nameof(UserRolesEnum.Auxiliar);
        }

        if (value.Contains("doc"))
        {
            return nameof(UserRolesEnum.Docente);
        }

        return nameof(UserRolesEnum.Invitado);
    }
}
