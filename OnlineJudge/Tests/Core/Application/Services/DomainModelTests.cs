using OnlineJudgeAdmin.Core.Application.Validators.Implementations;
using OnlineJudgeAdmin.Core.Domain.Models;

public class DomainModelTests
{
    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "A1")]
    [InlineData(27, "B1")]
    [InlineData(52, "A2")]
    public void ContestProblemCode_FromNumber_MapsNumbersToContestCodes(int number, string expectedCode)
    {
        Assert.Equal(expectedCode, ContestProblemCode.FromNumber(number));
    }

    [Fact]
    public void ContestProblemCode_FromNumber_RejectsNegativeNumbers()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ContestProblemCode.FromNumber(-1));
    }

    [Theory]
    [InlineData("A", true, 0)]
    [InlineData("z", true, 25)]
    [InlineData("A1", true, 26)]
    [InlineData(" B1 ", true, 27)]
    [InlineData("1A", false, -1)]
    [InlineData("A-1", false, -1)]
    [InlineData("", false, -1)]
    public void ContestProblemCode_TryParse_ParsesContestCodes(string value, bool expectedResult, int expectedNumber)
    {
        var result = ContestProblemCode.TryParse(value, out int number);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedNumber, number);
    }

    [Theory]
    [InlineData(JudgeResultCodes.Pending, "pending", "queued", false)]
    [InlineData(JudgeResultCodes.Accepted, "accepted", "finished", true)]
    [InlineData(JudgeResultCodes.WrongAnswer, "wrong_answer", "finished", true)]
    [InlineData(JudgeResultCodes.CompileError, "compile_error", "finished", true)]
    [InlineData(JudgeResultCodes.AiDetected, "ai_detected", "finished", true)]
    [InlineData(99, "unknown", "evaluating", false)]
    public void JudgeVerdictCatalog_Map_ReturnsCanonicalStatus(short resultCode, string expectedStatusKey, string expectedGeneralStatusKey, bool expectedIsFinal)
    {
        var verdict = JudgeVerdictCatalog.Map(resultCode);

        Assert.Equal(expectedStatusKey, verdict.StatusKey);
        Assert.Equal(expectedGeneralStatusKey, verdict.GeneralStatusKey);
        Assert.Equal(expectedIsFinal, verdict.IsFinal);
    }

    [Fact]
    public void ProblemValidator_RequiresDescription()
    {
        var validator = new ProblemValidator();

        var invalid = validator.Validate(new Problem { Description = string.Empty });
        var valid = validator.Validate(new Problem { Description = "Descripción" });

        Assert.False(invalid.IsValid);
        Assert.True(valid.IsValid);
    }
}
