using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Services;

public interface ISiteService
{
    Task<SiteSettings> GetAsync(int siteId);

    Task<SiteSettings> UpdateAsync(SiteSettings settings);
}
