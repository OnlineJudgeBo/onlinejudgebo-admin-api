using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public class SiteRepository : ISiteRepository
{
    private readonly AppDbContext _context;

    public SiteRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<SiteSettings?> GetAsync(int siteId)
    {
        var site = await _context.Sites.AsNoTracking()
            .Where(item => item.SiteId == siteId)
            .Select(item => new SiteSettings { SiteId = item.SiteId, Name = item.Name, LogoUrl = item.LogoUrl, PrimaryColor = item.PrimaryColor })
            .FirstOrDefaultAsync();
        if (site != null)
        {
            site.Domains = await _context.SiteDomains.AsNoTracking()
                .Where(item => item.SiteId == siteId)
                .OrderBy(item => item.Domain)
                .Select(item => item.Domain)
                .ToListAsync();
        }
        return site;
    }

    public Task<string?> FindDomainOfOtherSiteAsync(int siteId, IEnumerable<string> domains)
    {
        var list = domains.ToList();
        return _context.SiteDomains.AsNoTracking()
            .Where(item => item.SiteId != siteId && list.Contains(item.Domain))
            .Select(item => item.Domain)
            .FirstOrDefaultAsync();
    }

    public async Task UpdateAsync(SiteSettings settings)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.Sites.Where(item => item.SiteId == settings.SiteId).ExecuteUpdateAsync(setters => setters
            .SetProperty(item => item.Name, settings.Name)
            .SetProperty(item => item.LogoUrl, settings.LogoUrl)
            .SetProperty(item => item.PrimaryColor, settings.PrimaryColor));
        await _context.SiteDomains.Where(item => item.SiteId == settings.SiteId).ExecuteDeleteAsync();
        _context.SiteDomains.AddRange(settings.Domains.Select(domain => new DbSiteDomain { Domain = domain, SiteId = settings.SiteId }));
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
