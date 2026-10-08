using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.BocaImporter;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

[ApiController]
[Route("/api/problems")]
[Authorize(Roles = AuthorizationRoles.Administrador)]
public sealed class TaskImportController : ControllerBase
{
    private readonly OlympiadTaskImportService _import;
    private readonly CurrentUser _currentUser;

    public TaskImportController(OlympiadTaskImportService import, UserClaimsHelper claims)
    {
        _import = import ?? throw new ArgumentNullException(nameof(import));
        _currentUser = (claims ?? throw new ArgumentNullException(nameof(claims))).GetUserContextRole();
    }

    // Olympiad task in CMS (task.yaml) or TPS (problem.json) format.
    [HttpPost("import-task")]
    [RequestSizeLimit(200_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 200_000_000)]
    public async Task<IActionResult> ImportAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ErrorDetails { StatusCode = 400, Message = "Sube el .zip de la tarea (CMS o TPS)." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? _currentUser.UserId;
            return Ok(await _import.ImportAsync(userId, stream, _currentUser.SiteId, ReadStatementAsync));
        }
        catch (Exception error) when (error is InvalidDataException or System.Text.Json.JsonException)
        {
            return BadRequest(new ErrorDetails { StatusCode = 400, Message = error.Message });
        }
    }

    // Same PDF transcription the BOCA importer uses; a statement that cannot be read is left for the editor.
    private static async Task<TaskStatement?> ReadStatementAsync(string pdfPath)
    {
        var folder = Path.Combine(Path.GetTempPath(), "task-statement", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "description"));
            System.IO.File.Copy(pdfPath, Path.Combine(folder, "description", "statement.pdf"));
            var description = await BocaPackageReader.ReadDescriptionAsync(folder, "statement.pdf");
            return string.IsNullOrWhiteSpace(description.Html)
                ? null
                : new TaskStatement(description.Html, description.InputHtml, description.OutputHtml, description.HintHtml);
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
