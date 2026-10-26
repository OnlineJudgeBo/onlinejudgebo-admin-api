using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdminApi.DataTransferObjects;
using OnlineJudgeAdminApi.Helpers;

namespace OnlineJudgeAdminApi.Controllers;

// The branding and domains of the administrator's own site.
[ApiController]
[Route("/api/site")]
[Authorize(Roles = AuthorizationRoles.Administrador)]
public class SiteController : ControllerBase
{
    private readonly ISiteService _siteService;
    private readonly UserClaimsHelper _userClaimsHelper;

    public SiteController(ISiteService siteService, UserClaimsHelper userClaimsHelper)
    {
        _siteService = siteService ?? throw new ArgumentNullException(nameof(siteService));
        _userClaimsHelper = userClaimsHelper ?? throw new ArgumentNullException(nameof(userClaimsHelper));
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        return Ok(await _siteService.GetAsync(_userClaimsHelper.GetUserContextRole().SiteId));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateAsync(SiteForUpdate request)
    {
        return Ok(await _siteService.UpdateAsync(new SiteSettings
        {
            SiteId = _userClaimsHelper.GetUserContextRole().SiteId,
            Name = request.Name,
            LogoUrl = request.LogoUrl,
            PrimaryColor = request.PrimaryColor,
            Domains = request.Domains
        }));
    }
}
