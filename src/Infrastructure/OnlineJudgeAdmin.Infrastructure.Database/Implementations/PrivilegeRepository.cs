using AutoMapper;
using Microsoft.EntityFrameworkCore;
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

    public async Task CreatePrivilegeAsync(Privilege privilege)
    {
        DbPrivilege dbPrivilege = _mapper.Map<DbPrivilege>(privilege);
        _context.Privilege.Add(dbPrivilege);
        _context.SaveChanges();
    }

    public async Task<Privilege> GetUserPrivilegeAsync(string userId)
    {
        DbPrivilege dbPrivilege =  await _context.Privilege
            .Where(x => x.UserId == userId)
            .FirstOrDefaultAsync();
            return _mapper.Map<Privilege>(dbPrivilege);
    }
}
