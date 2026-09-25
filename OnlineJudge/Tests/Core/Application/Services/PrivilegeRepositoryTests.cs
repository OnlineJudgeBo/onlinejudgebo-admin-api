using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Models;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
using static RepositoryTestSupport;

public class PrivilegeRepositoryTests
{
    [Fact]
    public async Task CreatePrivilegeAsync_PersistsPrivilege()
    {
        using AppDbContext context = CreateContext();
        var repository = new PrivilegeRepository(context, CreateMapper());

        await repository.CreatePrivilegeAsync(new Privilege
        {
            UserId = "alice",
            Rightstr = "p1",
            Defunct = "N"
        });

        DbPrivilege saved = await context.Privilege.SingleAsync();
        Assert.Equal("alice", saved.UserId);
        Assert.Equal("p1", saved.Rightstr);
    }

    [Fact]
    public async Task GetUserPrivilegeAsync_ReturnsMatchingPrivilege()
    {
        using AppDbContext context = CreateContext();
        context.Privilege.Add(new DbPrivilege { UserId = "alice", Rightstr = "p1", Defunct = "N" });
        await context.SaveChangesAsync();
        var repository = new PrivilegeRepository(context, CreateMapper());

        Privilege result = await repository.GetUserPrivilegeAsync("alice");

        Assert.Equal("alice", result.UserId);
        Assert.Equal("p1", result.Rightstr);
    }
}
