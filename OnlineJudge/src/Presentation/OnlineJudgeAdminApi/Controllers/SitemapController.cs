using System.Security;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdminApi.Controllers;

// sitemap.xml for search engines: the public pages of a site, built from the same lists the site shows.
[ApiController]
[AllowAnonymous]
public class SitemapController : ControllerBase
{
    private const int PageSize = 100;
    private static readonly string[] StaticPaths = ["/", "/learn", "/topics", "/problems", "/contests", "/ranking"];

    private readonly IPublicService _publicService;
    private readonly ILearningPathService _learningPathService;

    public SitemapController(IPublicService publicService, ILearningPathService learningPathService)
    {
        _publicService = publicService ?? throw new ArgumentNullException(nameof(publicService));
        _learningPathService = learningPathService ?? throw new ArgumentNullException(nameof(learningPathService));
    }

    [HttpGet("/api/public/sitemap.xml")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> GetSitemapAsync([FromQuery] int siteId = 1)
    {
        var paths = new List<string>(StaticPaths);

        var guest = new CurrentUser { SiteId = siteId, UserId = "public", Role = UserRolesEnum.Invitado };
        paths.AddRange((await _learningPathService.GetLearningPathsAsync(siteId, guest))
            .Select(path => $"/topics/{Uri.EscapeDataString(path.Id)}"));

        for (var page = 1; ; page++)
        {
            var problems = await _publicService.GetProblemsAsync(siteId, page, PageSize, null, null, null, null, null, "id_asc", null);
            paths.AddRange(problems.Items.Where(item => item.ContestId == null).Select(item => $"/problems/{item.ProblemId}"));
            if (problems.Items.Count < PageSize) break;
        }

        for (var page = 1; ; page++)
        {
            var contests = await _publicService.GetContestsAsync(siteId, null, null, null, page, PageSize, null);
            paths.AddRange(contests.Items.Where(item => !item.IsPrivate).Select(item => $"/contests/{item.ContestId}"));
            if (contests.Items.Count < PageSize) break;
        }

        return Content(BuildSitemap($"https://{Request.Host}", paths.Distinct()), "application/xml; charset=utf-8");
    }

    // ponytail: the base URL comes from the Host header (the site's own domain behind the proxy), always https.
    public static string BuildSitemap(string baseUrl, IEnumerable<string> paths)
    {
        var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
        foreach (var path in paths)
            xml.Append("  <url><loc>").Append(SecurityElement.Escape(baseUrl + path)).Append("</loc></url>\n");
        return xml.Append("</urlset>\n").ToString();
    }
}
