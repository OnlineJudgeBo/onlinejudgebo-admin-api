using System.Text.RegularExpressions;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

public class SiteService : ISiteService
{
    private const int MaxDomains = 10;
    private static readonly Regex ColorPattern = new("^#[0-9a-fA-F]{6}$");
    private static readonly Regex DomainPattern = new("^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)*$");

    private readonly ISiteRepository _siteRepository;

    public SiteService(ISiteRepository siteRepository)
    {
        _siteRepository = siteRepository ?? throw new ArgumentNullException(nameof(siteRepository));
    }

    public async Task<SiteSettings> GetAsync(int siteId)
    {
        return await _siteRepository.GetAsync(siteId) ?? throw new KeyNotFoundException($"no existe el sitio {siteId}");
    }

    public async Task<SiteSettings> UpdateAsync(SiteSettings settings)
    {
        settings.Name = settings.Name?.Trim() ?? string.Empty;
        if (settings.Name.Length is 0 or > 100)
        {
            throw new ArgumentException("el nombre debe tener entre 1 y 100 caracteres");
        }

        settings.LogoUrl = string.IsNullOrWhiteSpace(settings.LogoUrl) ? null : settings.LogoUrl.Trim();
        if (settings.LogoUrl != null && (settings.LogoUrl.Length > 500
            || !Uri.TryCreate(settings.LogoUrl, UriKind.Absolute, out var logo) || (logo.Scheme != "https" && logo.Scheme != "http")))
        {
            throw new ArgumentException("el logo debe ser una URL http(s) de hasta 500 caracteres");
        }

        settings.PrimaryColor = string.IsNullOrWhiteSpace(settings.PrimaryColor) ? null : settings.PrimaryColor.Trim().ToLowerInvariant();
        if (settings.PrimaryColor != null && !ColorPattern.IsMatch(settings.PrimaryColor))
        {
            throw new ArgumentException("el color debe tener la forma #1e88e5");
        }

        settings.Domains = (settings.Domains ?? []).Select(SiteDomain.Normalize).Where(item => item.Length > 0).Distinct().ToList();
        if (settings.Domains.Count > MaxDomains)
        {
            throw new ArgumentException($"un sitio puede tener hasta {MaxDomains} dominios");
        }
        var invalid = settings.Domains.FirstOrDefault(item => item.Length > 255 || !DomainPattern.IsMatch(item));
        if (invalid != null)
        {
            throw new ArgumentException($"'{invalid}' no es un dominio válido");
        }
        var taken = await _siteRepository.FindDomainOfOtherSiteAsync(settings.SiteId, settings.Domains);
        if (taken != null)
        {
            throw new ArgumentException($"el dominio {taken} ya pertenece a otro sitio");
        }

        await GetAsync(settings.SiteId);
        await _siteRepository.UpdateAsync(settings);
        return settings;
    }
}
