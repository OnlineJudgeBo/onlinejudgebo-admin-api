using System.Net;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

public class BlockRuleService : IBlockRuleService
{
    // How far back a search looks in the access log.
    private const int SearchedRequests = 20_000;
    private const int MaxLimit = 1_000;
    // The Bloqueos page itself and its polling, which would otherwise fill the list.
    private static readonly string[] OwnPaths = ["/api/blockrules", "/admin/management/blocks"];

    private readonly IBlockRuleRepository _blockRuleRepository;
    private readonly IAccessLogReader _accessLogReader;

    public BlockRuleService(IBlockRuleRepository blockRuleRepository, IAccessLogReader accessLogReader)
    {
        _blockRuleRepository = blockRuleRepository ?? throw new ArgumentNullException(nameof(blockRuleRepository));
        _accessLogReader = accessLogReader ?? throw new ArgumentNullException(nameof(accessLogReader));
    }

    public Task<IEnumerable<BlockRule>> GetAllAsync()
    {
        return _blockRuleRepository.GetAllAsync();
    }

    public Task<BlockRule> AddAsync(string type, string value, string? note)
    {
        value = value?.Trim() ?? string.Empty;
        if (type != BlockRule.IpType && type != BlockRule.TextType)
        {
            throw new ArgumentException("el tipo debe ser 'ip' o 'text'");
        }
        if (value.Length is 0 or > 255)
        {
            throw new ArgumentException("el valor debe tener entre 1 y 255 caracteres");
        }
        if (type == BlockRule.IpType && !IPAddress.TryParse(value, out _) && !IPNetwork.TryParse(value, out _))
        {
            throw new ArgumentException("la IP debe ser una dirección o un rango CIDR, por ejemplo 190.129.0.0/16");
        }

        return _blockRuleRepository.AddAsync(new BlockRule { Type = type, Value = value, Note = note?.Trim(), CreatedAt = DateTime.Now });
    }

    public Task DeleteAsync(int id)
    {
        return _blockRuleRepository.DeleteAsync(id);
    }

    public async Task<IReadOnlyList<AccessLogEntry>> GetRecentRequestsAsync(string? ip, string? text, int limit)
    {
        limit = Math.Clamp(limit, 1, MaxLimit);
        var filtered = !string.IsNullOrWhiteSpace(ip) || !string.IsNullOrWhiteSpace(text);
        var entries = await _accessLogReader.ReadLastAsync(filtered ? SearchedRequests : limit);
        var rules = (await _blockRuleRepository.GetAllAsync()).ToList();

        var result = new List<AccessLogEntry>();
        foreach (var entry in entries)
        {
            if (OwnPaths.Any(path => entry.Url.StartsWith(path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var requestText = BlockRule.RequestText(entry.UserAgent, entry.Referer, entry.Url);
            if ((!string.IsNullOrWhiteSpace(ip) && !entry.Ip.StartsWith(ip.Trim(), StringComparison.Ordinal))
                || (!string.IsNullOrWhiteSpace(text) && !requestText.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            entry.Blocked = BlockRule.Blocks(rules, entry.Ip, requestText);
            result.Add(entry);
            if (result.Count == limit)
            {
                break;
            }
        }
        return result;
    }
}
