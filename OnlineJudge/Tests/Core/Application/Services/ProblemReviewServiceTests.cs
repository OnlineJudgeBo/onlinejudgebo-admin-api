using System.Text;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;

public class ProblemReviewServiceTests
{
    [Fact]
    public async Task Review_ReportsWhatIsMissing()
    {
        var problems = new Mock<IProblemRepository>();
        var files = new Mock<IFileSystemLocalManagerManager>();
        problems.Setup(item => item.GetProblemsForReviewAsync(3)).ReturnsAsync(
        [
            new ProblemReview { ProblemId = 1, HasDescription = true, HasInput = true, HasOutput = true, Samples = 1, Accepted = 4 },
            new ProblemReview { ProblemId = 2, HasDescription = true, Samples = 1, SpecialJudge = true },
            new ProblemReview { ProblemId = 3, HasDescription = true, HasInput = true, HasOutput = true, Samples = 1, Accepted = 1 },
        ]);
        files.Setup(item => item.ListFiles("1")).Returns(new[] { "sample.in", "sample.out", "1.in", "1.out", "2.in", "2.out", "scoring.json" });
        files.Setup(item => item.ReadFile("1", "scoring.json")).Returns(Encoding.UTF8.GetBytes(
            "{\"groups\":[{\"name\":\"a\",\"points\":40,\"tests\":[\"1\"]},{\"name\":\"b\",\"points\":60,\"tests\":[\"2\"]}]}"));
        files.Setup(item => item.ListFiles("2")).Returns(new[] { "sample.in", "sample.out", "extra.out", "scoring.json" });
        files.Setup(item => item.ReadFile("2", "scoring.json")).Returns(Encoding.UTF8.GetBytes(
            "{\"groups\":[{\"name\":\"a\",\"points\":50,\"tests\":[\"1\"]}]}"));
        files.Setup(item => item.ListFiles("3")).Returns(new[] { "1.in" });

        var reviews = await new ProblemReviewService(problems.Object, files.Object).ReviewAsync(3);

        Assert.Empty(reviews[0].Issues);
        Assert.Equal(2, reviews[0].Tests);
        Assert.Equal(
            new[] { "no-input", "no-output", "only-samples", "unpaired", "no-checker", "subtasks", "subtasks", "unsolved" },
            reviews[1].Issues.Select(issue => issue.Code));
        Assert.Equal("Salidas sin entrada: extra", reviews[1].Issues[3].Message);
        Assert.Equal("Entradas sin salida: 1", Assert.Single(reviews[2].Issues).Message);
    }
}
