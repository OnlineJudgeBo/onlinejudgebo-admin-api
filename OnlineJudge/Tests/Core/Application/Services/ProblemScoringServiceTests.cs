using System.Text;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

public class ProblemScoringServiceTests
{
    private readonly Mock<IProblemService> _problems = new();
    private readonly Mock<IFileSystemLocalManagerManager> _files = new();

    public ProblemScoringServiceTests()
    {
        _problems.Setup(item => item.GetProblemByIdAsync(5, 1)).ReturnsAsync(new Problem { ProblemId = 5 });
        _files.Setup(item => item.ListFiles("5")).Returns(new[] { "sample.in", "sample2.in", "s1_01.in", "s1_01.out", "s1_02.in", "s2_01.in", "checker.cpp" });
    }

    private ProblemScoringService Service() => new(_problems.Object, _files.Object);

    private static ProblemScoring Groups(params ProblemScoreGroup[] groups) => new() { Groups = groups.ToList() };

    [Theory]
    [InlineData("s1_*", "s1_01", true)]
    [InlineData("s1_*", "s2_01", false)]
    [InlineData("s?_01", "s2_01", true)]
    [InlineData("s[12]_01", "s3_01", false)]
    [InlineData("s1.01", "s1_01", false)]
    public void Matches_FollowsGlobRules(string pattern, string test, bool expected)
    {
        Assert.Equal(expected, ProblemScoringService.Matches(pattern, test));
    }

    [Fact]
    public async Task Save_WritesNormalizedGroupsTheKernelCanRead()
    {
        string? written = null;
        _files.Setup(item => item.WriteToFile("5", "scoring.json", It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => written = content);

        var saved = await Service().SaveAsync(5, 1, Groups(
            new ProblemScoreGroup { Points = 10, Tests = ["s1_*"] },
            new ProblemScoreGroup { Name = " Difícil ", Points = 20, Type = "MIN", Tests = [" s2_* ", ""] }));

        Assert.Equal(new[] { "s1_01", "s1_02", "s2_01" }, saved.AvailableTests);
        Assert.Equal(new[] { "Grupo 1", "Difícil" }, saved.Groups.Select(group => group.Name));
        var roundTrip = ProblemScoringService.Parse(Encoding.UTF8.GetBytes(written!));
        Assert.Equal(new[] { "sum", "min" }, roundTrip.Groups.Select(group => group.Type));
        Assert.Equal(new[] { "s2_*" }, roundTrip.Groups[1].Tests);
        Assert.Contains("\"groups\"", written);
    }

    [Theory]
    [InlineData(0, "sum", "s1_*")]
    [InlineData(10, "max", "s1_*")]
    [InlineData(10, "sum", "s9_*")]
    [InlineData(10, "sum", "sample*")]
    public async Task Save_RejectsGroupsThatCouldNeverScore(int points, string type, string pattern)
    {
        var scoring = Groups(new ProblemScoreGroup { Points = points, Type = type, Tests = [pattern] });

        await Assert.ThrowsAsync<ArgumentException>(() => Service().SaveAsync(5, 1, scoring));

        _files.Verify(item => item.WriteToFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Save_WithoutGroupsRemovesTheFile_AndOtherSitesCannotTouchIt()
    {
        await Service().SaveAsync(5, 1, Groups());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service().SaveAsync(5, 2, Groups()));

        _files.Verify(item => item.DeleteFile("5", "scoring.json"), Times.Once);
    }

    [Fact]
    public async Task Get_ReturnsStoredGroups_AndIgnoresABrokenFile()
    {
        _files.Setup(item => item.ListFiles("5")).Returns(new[] { "s1_01.in", "scoring.json" });
        _files.Setup(item => item.ReadFile("5", "scoring.json"))
            .Returns(Encoding.UTF8.GetBytes("{\"groups\":[{\"name\":\"A\",\"points\":40,\"type\":\"mul\",\"tests\":[\"s1_*\"]}]}"));
        var stored = await Service().GetAsync(5, 1);
        _files.Setup(item => item.ReadFile("5", "scoring.json")).Returns(Encoding.UTF8.GetBytes("not json"));
        var broken = await Service().GetAsync(5, 1);

        Assert.Equal(40, Assert.Single(stored.Groups).Points);
        Assert.Equal(new[] { "s1_01" }, stored.AvailableTests);
        Assert.Empty(broken.Groups);
    }
}
