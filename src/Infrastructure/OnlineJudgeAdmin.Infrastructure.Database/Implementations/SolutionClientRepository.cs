using AutoMapper;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public class SolutionClientRepository : ISolutionClientRepository
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public SolutionClientRepository(AppDbContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<int> SaveRemoteSolutionAsync(int solutionId, int clientId)
    {
        DbSolutionClient solutionClient = new DbSolutionClient {
            SolutionId = solutionId,
            ClientId = clientId,
            AssignedDate = DateTime.Now,
        };

        await _context.DbSolutionClient.AddAsync(solutionClient);
        await _context.SaveChangesAsync();
        return solutionClient.Id;
    }
}
