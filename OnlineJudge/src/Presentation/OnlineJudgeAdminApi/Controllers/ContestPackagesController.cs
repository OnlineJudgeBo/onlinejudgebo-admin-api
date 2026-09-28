using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/contests")]
[Authorize(Roles = AuthorizationRoles.Administrador)]
public sealed class ContestPackagesController : ControllerBase
{
    private readonly ContestPackageService _packages;
    private readonly CurrentUser _currentUser;

    public ContestPackagesController(ContestPackageService packages, UserClaimsHelper claims)
    {
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _currentUser = (claims ?? throw new ArgumentNullException(nameof(claims))).GetUserContextRole();
    }

    [HttpGet("{contestId:int}/export")]
    public async Task<IActionResult> ExportAsync(int contestId)
    {
        try
        {
            var bytes = await _packages.ExportAsync(contestId, _currentUser.SiteId);
            return new FileContentResult(bytes, "application/zip") { FileDownloadName = $"contest-{contestId}.zip" };
        }
        catch (KeyNotFoundException error)
        {
            return new NotFoundObjectResult(new ErrorDetails { StatusCode = 404, Message = error.Message });
        }
        catch (Exception error) when (error is InvalidOperationException or InvalidDataException)
        {
            return new BadRequestObjectResult(new ErrorDetails { StatusCode = 400, Message = error.Message });
        }
    }

    [HttpPost("import")]
    [RequestSizeLimit(200_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 200_000_000)]
    public async Task<IActionResult> ImportAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return new BadRequestObjectResult(new ErrorDetails { StatusCode = 400, Message = "Sube un archivo .zip de concurso." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            return new OkObjectResult(await _packages.ImportAsync(_currentUser.UserId, stream, _currentUser.SiteId));
        }
        catch (Exception error) when (error is InvalidOperationException or InvalidDataException or JsonException)
        {
            return new BadRequestObjectResult(new ErrorDetails { StatusCode = 400, Message = error.Message });
        }
    }
}
