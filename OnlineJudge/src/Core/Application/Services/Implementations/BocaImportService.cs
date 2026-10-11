using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

public class BocaImportService(
    IProblemService problemService,
    IProblemClassifierService classifierService,
    IFileSystemLocalManagerManager fileManager,
    IBocaPackageStaging staging) : IBocaImportService
{
    public async Task<BocaPreview> PreviewAsync(Stream zip, string fileName)
    {
        var (stagingId, package) = await staging.StageAsync(zip, fileName);
        try
        {
            var problem = BuildProblem(package);
            var suggestion = await classifierService.SuggestClassificationsAsync(problem);
            return new BocaPreview(stagingId, problem, package.ReviewReasons, package.TestCases.Count, suggestion);
        }
        catch
        {
            staging.Discard(stagingId);
            throw;
        }
    }

    public async Task<int?> ConfirmAsync(string stagingId, string userId, int siteId, IReadOnlyList<int>? classificationIds)
    {
        try
        {
            var package = await staging.OpenAsync(stagingId)
                ?? throw new InvalidOperationException("No se encontró el paquete preparado (puede haber expirado) - vuelve a subirlo.");

            var problem = BuildProblem(package);
            if (classificationIds is { Count: > 0 })
            {
                problem.Classifications = classificationIds.Select(id => new Classification { ClassificationId = id }).ToList();
            }

            var created = await problemService.CreateProblemAsync(userId, problem, siteId);

            var problemId = created.ProblemId!.Value.ToString();
            for (var i = 0; i < package.TestCases.Count; i++)
            {
                var caseNumber = i + 1;
                fileManager.WriteToFile(problemId, $"{caseNumber}.in", package.TestCases[i].Input);
                fileManager.WriteToFile(problemId, $"{caseNumber}.out", package.TestCases[i].Output);
            }

            staging.Archive(stagingId, problemId);
            return created.ProblemId;
        }
        finally
        {
            staging.Discard(stagingId);
        }
    }

    private static Problem BuildProblem(BocaPackage package) => new()
    {
        Title = package.FullName,
        Description = package.DescriptionHtml,
        Input = package.InputHtml,
        Output = package.OutputHtml,
        SampleInput = package.SampleInput,
        SampleOutput = package.SampleOutput,
        Hint = package.HintHtml,
        Source = $"BOCA import ({package.BaseName})",
        OriginSource = "BOCA import",
        TimeLimit = package.TimeLimitSeconds,
        MemoryLimit = package.MemoryLimitMb,
        Spj = "N",
        Defunct = "N",
        InDate = DateTime.Now,
    };
}
