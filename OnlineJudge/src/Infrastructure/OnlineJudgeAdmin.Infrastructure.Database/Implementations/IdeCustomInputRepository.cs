using Microsoft.EntityFrameworkCore;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models.IdeIntegration;
using OnlineJudgeAdmin.Infrastructure.Database.Models;

namespace OnlineJudgeAdmin.Infrastructure.Database.Implementations;

public sealed class IdeCustomInputRepository : IIdeCustomInputRepository
{
    private readonly AppDbContext _dbContext;

    public IdeCustomInputRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<int> CreateCustomInputRunAsync(IdeCustomInputRunCreation run)
    {
        await ValidateCustomInputTargetAsync(run);

        DateTime now = DateTime.Now;
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync();

        DbSolution solution = new DbSolution
        {
            ProblemId = run.ProblemId,
            UserId = run.UserId,
            Time = 0,
            Memory = 0,
            InDate = now,
            Result = 0,
            Language = (uint)run.LanguageId,
            Ip = "0.0.0.0",
            ContestId = run.ContestId,
            Num = run.Num,
            CodeLength = run.SourceCode.Length,
            PassRate = 0,
            IsRemoteOj = false,
            RemoteId = 0,
            SiteId = run.SiteId
        };

        await _dbContext.Solutions.AddAsync(solution);
        await _dbContext.SaveChangesAsync();

        await _dbContext.SourceCodes.AddAsync(new DbSourceCode
        {
            SolutionId = solution.SolutionId,
            Source = run.SourceCode
        });

        await _dbContext.CustomInputs.AddAsync(new DbCustomInput
        {
            SolutionId = solution.SolutionId,
            ProblemId = run.ProblemId,
            UserId = run.UserId,
            SiteId = run.SiteId,
            CreatedAt = now
        });

        foreach (DbCustomInputCase customCase in BuildCustomInputCases(solution.SolutionId, run))
        {
            await _dbContext.CustomInputCases.AddAsync(customCase);
        }

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        return solution.SolutionId;
    }

    public async Task<bool> IsCustomInputAsync(int solutionId)
    {
        return await _dbContext.CustomInputs.AnyAsync(item => item.SolutionId == solutionId);
    }

    private async Task ValidateCustomInputTargetAsync(IdeCustomInputRunCreation run)
    {
        bool userExists = await _dbContext.Users.AnyAsync(user => user.UserId == run.UserId && user.SiteId == run.SiteId && !user.IsDeleted && user.IsActive);
        if (!userExists)
        {
            throw new ArgumentException("Usuario inválido para este sitio.");
        }

        bool problemExists = await _dbContext.ProblemSites.AnyAsync(problemSite => problemSite.problemId == run.ProblemId && problemSite.SiteId == run.SiteId && problemSite.IsActive);
        if (!problemExists)
        {
            throw new ArgumentException("Problema inválido o no disponible.");
        }

        bool languageExists = await _dbContext.ProgrammingLanguages.AnyAsync(language => language.LanguageId == run.LanguageId);
        if (!languageExists)
        {
            throw new ArgumentException("LanguageId no soportado.");
        }
    }

    private static IReadOnlyCollection<DbCustomInputCase> BuildCustomInputCases(int solutionId, IdeCustomInputRunCreation run)
    {
        var cases = run.Testcases
            .Where(item => !string.IsNullOrEmpty(item.Input) || !string.IsNullOrEmpty(item.ExpectedOutput))
            .Select((item, index) => new DbCustomInputCase
            {
                SolutionId = solutionId,
                CaseNumber = index + 1,
                InputText = item.Input ?? string.Empty,
                ExpectedOutput = item.ExpectedOutput
            })
            .ToList();

        if (cases.Count > 0)
        {
            return cases;
        }

        return new[]
        {
            new DbCustomInputCase
            {
                SolutionId = solutionId,
                CaseNumber = 1,
                InputText = run.Stdin ?? string.Empty,
                ExpectedOutput = null
            }
        };
    }
}
