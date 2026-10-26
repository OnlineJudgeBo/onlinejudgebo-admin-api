using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;

public interface ISiteRepository
{
    Task<SiteSettings?> GetAsync(int siteId);

    // The first of these domains that already opens another site, or null.
    Task<string?> FindDomainOfOtherSiteAsync(int siteId, IEnumerable<string> domains);

    Task UpdateAsync(SiteSettings settings);
}
