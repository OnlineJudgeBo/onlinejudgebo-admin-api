namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

// Country of an IP address as an ISO 3166 code ("BO"), or null when it is unknown.
public interface IIpCountryLookup
{
    string? CountryOf(string ip);
}
