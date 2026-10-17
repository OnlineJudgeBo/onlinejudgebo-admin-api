using System.Net;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

public class BlockRuleService : IBlockRuleService
{
    private readonly IBlockRuleRepository _blockRuleRepository;

    public BlockRuleService(IBlockRuleRepository blockRuleRepository)
    {
        _blockRuleRepository = blockRuleRepository ?? throw new ArgumentNullException(nameof(blockRuleRepository));
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
}
