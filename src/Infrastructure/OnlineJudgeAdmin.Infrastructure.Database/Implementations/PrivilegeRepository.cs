using AutoMapper;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public class PrivilegeRepository : IPrivilegeRepository
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public PrivilegeRepository(AppDbContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async void CreatePrivilegeAsync(Privilege privilege)
    {
        DbPrivilege dbPrivilege = _mapper.Map<DbPrivilege>(privilege);
        _context.Privilege.Add(dbPrivilege);
        _context.SaveChanges();
    }
}
