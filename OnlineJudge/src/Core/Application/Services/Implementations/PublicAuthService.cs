using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Core.Application.Services.Helpers;
using static OnlineJudgeAdmin.Core.Application.Services.Helpers.PublicRequestRules;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

public class PublicAuthService : IPublicAuthService
{
    private readonly IPublicRepository _publicRepository;
    private readonly IPasswordRecoveryEmailService _passwordRecoveryEmailService;
    private readonly IWelcomeEmailService _welcomeEmailService;
    private readonly IConfiguration _configuration;

    public PublicAuthService(
        IPublicRepository publicRepository,
        IPasswordRecoveryEmailService passwordRecoveryEmailService,
        IWelcomeEmailService welcomeEmailService,
        IConfiguration configuration)
    {
        _publicRepository = publicRepository ?? throw new ArgumentNullException(nameof(publicRepository));
        _passwordRecoveryEmailService = passwordRecoveryEmailService ?? throw new ArgumentNullException(nameof(passwordRecoveryEmailService));
        _welcomeEmailService = welcomeEmailService ?? throw new ArgumentNullException(nameof(welcomeEmailService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public Task<PublicAuthenticatedUser> LoginAsync(string userOrEmail, string password, int siteId, string clientIp = "0.0.0.0")
    {
        ValidateSite(siteId);

        if (string.IsNullOrWhiteSpace(userOrEmail) || string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Credenciales inválidas.");
        }

        return _publicRepository.LoginAsync(userOrEmail.Trim(), password, siteId, clientIp);
    }

    public Task<PublicAuthenticatedUser> RegisterAsync(string userId, string password, string email, string? nick, string? lastName, string? school, int siteId, string ipAddress)
    {
        ValidateSite(siteId);

        if (string.IsNullOrWhiteSpace(userId) || !System.Text.RegularExpressions.Regex.IsMatch(userId, "^[A-Za-z0-9_]{3,20}$"))
        {
            throw new ArgumentException("UserId inválido. Debe tener 3-20 caracteres alfanuméricos o _.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            throw new ArgumentException("La contraseña debe tener al menos 6 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new ArgumentException("Correo electrónico inválido.");
        }

        return RegisterInternalAsync(userId.Trim(), password, email.Trim(), TrimOrNull(nick), TrimOrNull(lastName), TrimOrNull(school), siteId, ipAddress);
    }

    public async Task RequestPasswordRecoveryAsync(string email, int siteId)
    {
        ValidateSite(siteId);

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Correo electrónico requerido.");
        }

        var normalizedEmail = email.Trim();

        if (!normalizedEmail.Contains('@'))
        {
            throw new ArgumentException("Correo electrónico inválido.");
        }

        // Always respond the same way whether or not the email is registered - a distinguishable
        // response here lets an attacker enumerate valid accounts one request at a time.
        var target = await _publicRepository.GetPasswordRecoveryTargetAsync(normalizedEmail, siteId);
        if (target == null)
        {
            return;
        }

        var recoveryCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var tokenTtlMinutes = 30;
        if (int.TryParse(_configuration["Email:PasswordRecovery:TokenTtlMinutes"] ?? _configuration["PasswordRecovery:TokenTtlMinutes"], out var configuredTtlMinutes))
        {
            tokenTtlMinutes = configuredTtlMinutes;
        }

        tokenTtlMinutes = Math.Max(tokenTtlMinutes, 5);
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(tokenTtlMinutes);

        await _publicRepository.SavePasswordRecoveryTokenAsync(target.UserId, siteId, ComputeRecoveryCodeHash(recoveryCode), expiresAtUtc);
        await _passwordRecoveryEmailService.SendRecoveryCodeAsync(target.Email, target.UserId, recoveryCode, siteId, target.Nick);
    }

    public async Task ResetPasswordWithRecoveryCodeAsync(string email, string recoveryCode, int siteId)
    {
        ValidateSite(siteId);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(recoveryCode))
        {
            throw new ArgumentException("Correo y código de recuperación son requeridos.");
        }

        var normalizedEmail = email.Trim();
        var normalizedCode = recoveryCode.Trim().ToUpperInvariant();
        if (!normalizedEmail.Contains('@'))
        {
            throw new ArgumentException("Correo electrónico inválido.");
        }

        if (normalizedCode.Length < 6)
        {
            throw new ArgumentException("Código de recuperación inválido.");
        }

        var passwordHash = GeneratePasswordHash(normalizedCode);
        var wasReset = await _publicRepository.ResetPasswordWithTokenAsync(
            normalizedEmail,
            siteId,
            ComputeRecoveryCodeHash(normalizedCode),
            DateTime.UtcNow,
            passwordHash);

        if (!wasReset)
        {
            throw new UnauthorizedAccessException("The recovery code is invalid or has expired.");
        }
    }

    public Task<PublicAuthenticatedUser> GetAuthenticatedUserAsync(CurrentUser currentUser)
    {
        ValidateCurrentUser(currentUser);
        return _publicRepository.GetAuthenticatedUserAsync(currentUser.UserId, currentUser.SiteId);
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string ComputeRecoveryCodeHash(string recoveryCode)
    {
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(recoveryCode))).ToLowerInvariant();
    }

    private async Task<PublicAuthenticatedUser> RegisterInternalAsync(string userId, string password, string email, string? nick, string? lastName, string? school, int siteId, string ipAddress)
    {
        var passwordHash = GeneratePasswordHash(password);
        var user = await _publicRepository.RegisterAsync(userId, passwordHash, email, nick, lastName, school, siteId, ipAddress);

        try
        {
            await _welcomeEmailService.SendWelcomeAsync(user.Email, user.UserId, siteId, user.Nick);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[PublicAuthService] No se pudo enviar el correo de bienvenida. UserId={user.UserId} Email={user.Email} SiteId={siteId}");
            Console.Error.WriteLine(ex.ToString());
        }

        return user;
    }

    private static string GeneratePasswordHash(string password)
    {
        return LegacyPasswordHash.Generate(password);
    }
}
