using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Infrastructure.Database.Implementations;
using OnlineJudgeAdmin.Infrastructure.Database.Models;
using static RepositoryTestSupport;

public class SolutionClientRepositoryTests
{
    [Fact]
    public async Task SaveRemoteSolutionAsync_AssignsGeneratedId_AndPersistsFields()
    {
        using AppDbContext context = CreateContext();
        var repository = new SolutionClientRepository(context, CreateMapper());

        int id = await repository.SaveRemoteSolutionAsync(solutionId: 42, clientId: 7);

        Assert.NotEqual(0, id);
        DbSolutionClient saved = await context.DbSolutionClient.SingleAsync();
        Assert.Equal(id, saved.Id);
        Assert.Equal(42, saved.SolutionId);
        Assert.Equal(7, saved.ClientId);
    }

    [Fact]
    public async Task SaveRemoteSolutionAsync_CalledTwice_ProducesDistinctIds()
    {
        using AppDbContext context = CreateContext();
        var repository = new SolutionClientRepository(context, CreateMapper());

        int first = await repository.SaveRemoteSolutionAsync(solutionId: 1, clientId: 1);
        int second = await repository.SaveRemoteSolutionAsync(solutionId: 2, clientId: 1);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task SaveSourceCodeAsync_PersistsSourceCode()
    {
        using AppDbContext context = CreateContext();
        var repository = new SolutionClientRepository(context, CreateMapper());

        await repository.SaveSourceCodeAsync(solutionId: 42, sourceCode: "print(1)");

        DbSourceCode saved = await context.SourceCodes.SingleAsync();
        Assert.Equal(42, saved.SolutionId);
        Assert.Equal("print(1)", saved.Source);
    }
}
