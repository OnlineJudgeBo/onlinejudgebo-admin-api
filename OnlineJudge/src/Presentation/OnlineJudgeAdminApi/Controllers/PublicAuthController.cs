using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/public/auth")]
public class PublicAuthController : ControllerBase
{
    private readonly IPublicService _publicService;
    private readonly UserClaimsHelper _userClaimsHelper;
    private readonly IConfiguration _configuration;

    public PublicAuthController(IPublicService publicService, UserClaimsHelper userClaimsHelper, IConfiguration configuration)
    {
        _publicService = publicService ?? throw new ArgumentNullException(nameof(publicService));
        _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync(PublicLoginForCreation loginForCreation)
    {
        try
        {
            var user = await _publicService.LoginAsync(loginForCreation.UserId, loginForCreation.Password, loginForCreation.SiteId);
            return Ok(CreateAuthResponse(user));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ErrorDetails { StatusCode = 401, Message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorDetails { StatusCode = 400, Message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync(PublicRegisterForCreation registerForCreation)
    {
        try
        {
            var user = await _publicService.RegisterAsync(
                registerForCreation.UserId,
                registerForCreation.Password,
                registerForCreation.Email,
                registerForCreation.Nick,
                registerForCreation.LastName,
                registerForCreation.School,
                registerForCreation.SiteId,
                ClientIpHelper.GetClientIp(HttpContext));

            return Ok(CreateAuthResponse(user));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorDetails { StatusCode = 409, Message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorDetails { StatusCode = 400, Message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("password-recovery/request")]
    public async Task<IActionResult> RequestPasswordRecoveryAsync(PublicPasswordRecoveryRequestForCreation request)
    {
        try
        {
            await _publicService.RequestPasswordRecoveryAsync(request.Email, request.SiteId);
            return Ok(new { message = "Revisa tu correo. Te enviamos un código de recuperación." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorDetails { StatusCode = 400, Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new ErrorDetails { StatusCode = 500, Message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("password-recovery/confirm")]
    public async Task<IActionResult> ConfirmPasswordRecoveryAsync(PublicPasswordRecoveryConfirmForCreation request)
    {
        try
        {
            await _publicService.ResetPasswordWithRecoveryCodeAsync(request.Email, request.RecoveryCode, request.SiteId);
            return Ok(new { message = "Contraseña temporal activada. Ingresa con el código enviado y luego cámbiala desde tu perfil." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ErrorDetails { StatusCode = 401, Message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorDetails { StatusCode = 400, Message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> MeAsync()
    {
        var currentUser = _userClaimsHelper.GetUserContextRole();
        return Ok(await _publicService.GetAuthenticatedUserAsync(currentUser));
    }

    private PublicAuthResponse CreateAuthResponse(PublicAuthenticatedUser user)
    {
        var expiresHours = Math.Max(_configuration.GetValue<int?>("Jwt:ExpiresHours") ?? 8, 1);
        var expiresAtUtc = DateTime.UtcNow.AddHours(expiresHours);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing.")));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("site_id", user.SiteId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            },
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new PublicAuthResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAtUtc = expiresAtUtc,
            User = user
        };
    }

}
