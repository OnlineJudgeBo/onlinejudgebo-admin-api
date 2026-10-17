using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public class BlockRuleRepository : IBlockRuleRepository
{
    private readonly AppDbContext _context;

    public BlockRuleRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IEnumerable<BlockRule>> GetAllAsync()
    {
        return await _context.BlockRules.AsNoTracking()
            .OrderByDescending(r => r.Id)
            .Select(r => new BlockRule { Id = r.Id, Type = r.Type, Value = r.Value, Note = r.Note, CreatedAt = r.CreatedAt })
            .ToListAsync();
    }

    public async Task<BlockRule> AddAsync(BlockRule rule)
    {
        var row = new DbBlockRule { Type = rule.Type, Value = rule.Value, Note = rule.Note, CreatedAt = rule.CreatedAt };
        _context.BlockRules.Add(row);
        await _context.SaveChangesAsync();
        rule.Id = row.Id;
        return rule;
    }

    public async Task DeleteAsync(int id)
    {
        if (await _context.BlockRules.Where(r => r.Id == id).ExecuteDeleteAsync() == 0)
        {
            throw new KeyNotFoundException($"no existe la regla {id}");
        }
    }
}
