using System.Net;
using MaxMind.Db;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

namespace OnlineJudgeAdmin.Infrastructure.FileSystemLocalManager;

// Reads the free DB-IP "IP to Country Lite" database (BlockRules:CountryDb, downloaded by the Dockerfile).
// Without the file every country is unknown. Data by DB-IP (https://db-ip.com), CC BY 4.0.
public sealed class DbIpCountryLookup : IIpCountryLookup, IDisposable
{
    private const string DefaultPath = "/app/geo/dbip-country-lite.mmdb";
    private readonly Reader? _reader;

    public DbIpCountryLookup(IConfiguration configuration)
    {
        var path = configuration["BlockRules:CountryDb"] ?? DefaultPath;
        try
        {
            _reader = File.Exists(path) ? new Reader(path, FileAccessMode.Memory) : null;
        }
        catch (InvalidDatabaseException)
        {
            _reader = null; // a broken download leaves the countries out instead of the API
        }
    }

    public string? CountryOf(string ip)
    {
        if (_reader == null || !IPAddress.TryParse(ip, out var address))
        {
            return null;
        }

        var record = _reader.Find<Dictionary<string, object>>(address);
        return record?.GetValueOrDefault("country") is Dictionary<string, object> country
            ? country.GetValueOrDefault("iso_code") as string
            : null;
    }

    public void Dispose()
    {
        _reader?.Dispose();
    }
}
