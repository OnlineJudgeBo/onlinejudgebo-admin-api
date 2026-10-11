using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

// Read-only check of every problem: what is missing in its statement and in its judge files.
public sealed class ProblemReviewService(IProblemRepository problems, IFileSystemLocalManagerManager files)
{
    private static readonly string[] Checkers = ["checker.cpp", "checker_cms.cpp", "spj"];

    // Without a site, the problems of every site.
    public async Task<List<ProblemReview>> ReviewAsync(int? siteId)
    {
        var reviews = await problems.GetProblemsForReviewAsync(siteId);
        foreach (var review in reviews)
        {
            Review(review);
        }

        return reviews;
    }

    private void Review(ProblemReview review)
    {
        void Add(string code, string message) => review.Issues.Add(new ProblemIssue(code, message));

        if (!review.HasDescription) Add("no-description", "Sin descripción");
        if (!review.HasInput) Add("no-input", "Sin descripción de la entrada");
        if (!review.HasOutput) Add("no-output", "Sin descripción de la salida");
        if (review.Samples == 0) Add("no-samples", "Sin casos de ejemplo");

        var folder = review.ProblemId.ToString();
        var names = files.ListFiles(folder);
        var inputs = names.Where(name => name.EndsWith(".in", StringComparison.Ordinal)).Select(name => name[..^3]).ToList();
        var outputs = names.Where(name => name.EndsWith(".out", StringComparison.Ordinal)).Select(name => name[..^4]).ToList();
        var tests = ProblemScoringService.AvailableTests(names);
        review.Tests = tests.Count;

        if (inputs.Count == 0)
        {
            Add("no-tests", "Sin casos de prueba: ningún envío se puede juzgar");
        }
        else if (tests.Count == 0)
        {
            Add("only-samples", "Solo se juzga con los casos de ejemplo");
        }

        if (inputs.Except(outputs).Order(StringComparer.Ordinal).ToList() is { Count: > 0 } alone)
        {
            Add("unpaired", "Entradas sin salida: " + string.Join(", ", alone));
        }

        if (outputs.Except(inputs).Order(StringComparer.Ordinal).ToList() is { Count: > 0 } orphans)
        {
            Add("unpaired", "Salidas sin entrada: " + string.Join(", ", orphans));
        }

        if (review.SpecialJudge && !names.Any(name => Checkers.Contains(name)))
        {
            Add("no-checker", "Juez especial sin checker");
        }

        if (names.Contains(ProblemScoringService.FileName))
        {
            var groups = ProblemScoringService.Parse(files.ReadFile(folder, ProblemScoringService.FileName)).Groups;
            if (groups.Sum(group => group.Points) is var total && total != 100)
            {
                Add("subtasks", $"Las subtareas suman {total:0.##} puntos, no 100");
            }

            foreach (var group in groups.Where(group => !tests.Any(test => (group.Tests ?? []).Any(pattern => ProblemScoringService.Matches(pattern, test)))))
            {
                Add("subtasks", $"La subtarea {group.Name} no toma ningún caso");
            }
        }

        if (review.Accepted == 0) Add("unsolved", "Nadie lo ha resuelto todavía");
    }
}
