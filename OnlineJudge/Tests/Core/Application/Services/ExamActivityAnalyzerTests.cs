using OnlineJudgeAdmin.Core.Application.Services.Helpers;
using OnlineJudgeAdmin.Core.Domain.Models;

public class ExamActivityAnalyzerTests
{
    private static readonly DateTime Start = new(2026, 9, 25, 8, 0, 0);

    private static Contest Exam(string? labIps = null) => new()
    {
        ContestId = 7, Title = "Parcial", StartTime = Start, EndTime = Start.AddHours(2), IsExam = true, ExamLabIps = labIps
    };

    private static ExamActivityEvent Login(string user, string ip, int minute) => new(user, ip, Start.AddMinutes(minute), ExamActivitySources.Login);

    private static ExamActivityEvent Submit(string user, string ip, int minute) => new(user, ip, Start.AddMinutes(minute), ExamActivitySources.Submission);

    private static ExamMonitorResponse Analyze(string? labIps, IEnumerable<string> participants, params ExamActivityEvent[] events) =>
        ExamActivityAnalyzer.Analyze(Exam(labIps), new ExamActivity { ParticipantUserIds = participants.ToList(), Events = events }, Start.AddHours(3));

    private static string[] Codes(ExamMonitorResponse response, string user) =>
        response.Participants.Single(participant => participant.UserId == user).AlertCodes.OrderBy(code => code).ToArray();

    [Theory]
    [InlineData("200.87.1.10", "200.87.1.10")]
    [InlineData(" 200.87.1.10 ,200.87.1.0/24\n200.87.1.10", "200.87.1.10, 200.87.1.0/24")]
    [InlineData("2001:db8::/32; 10.0.0.1", "2001:db8::/32, 10.0.0.1")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeLabIps_AcceptsIpsAndCidrs(string? input, string? expected)
    {
        Assert.Equal(expected, ExamActivityAnalyzer.NormalizeLabIps(input));
    }

    [Theory]
    [InlineData("laboratorio")]
    [InlineData("300.1.1.1")]
    [InlineData("10.0.0.0/40")]
    public void NormalizeLabIps_RejectsInvalidEntries(string input)
    {
        var error = Assert.Throws<ArgumentException>(() => ExamActivityAnalyzer.NormalizeLabIps(input));

        Assert.Contains("inválido", error.Message);
    }

    [Fact]
    public void NoLab_SingleIpPerUserHasNoAlerts()
    {
        var result = Analyze(null, new[] { "ana", "bob" }, Login("ana", "181.1.1.1", -10), Submit("ana", "181.1.1.1", 20), Submit("bob", "190.2.2.2", 30));

        Assert.Empty(result.Alerts);
        Assert.All(result.Participants, participant => Assert.Empty(participant.AlertCodes));
    }

    [Fact]
    public void NoLab_MovingToAnotherIpIsMultipleIps()
    {
        var result = Analyze(null, new[] { "ana" }, Login("ana", "181.1.1.1", 0), Submit("ana", "190.2.2.2", 40));

        var alert = Assert.Single(result.Alerts);
        Assert.Equal(ExamAlertCodes.MultipleIps, alert.Code);
        Assert.Equal("high", alert.Severity);
        Assert.Equal(new[] { "181.1.1.1", "190.2.2.2" }, alert.Ips);
    }

    [Fact]
    public void GoingBackAndForthBetweenIpsIsConcurrentUse()
    {
        var result = Analyze(null, new[] { "ana" },
            Login("ana", "181.1.1.1", 0), Login("ana", "190.2.2.2", 10), Submit("ana", "181.1.1.1", 20), Submit("ana", "190.2.2.2", 30));

        Assert.Equal(new[] { ExamAlertCodes.ConcurrentUse }, Codes(result, "ana"));
        Assert.Contains("dos lugares", result.Alerts.Single().Message);
    }

    [Fact]
    public void Lab_ActivityOnlyFromLabHasNoAlertsEvenWhenShared()
    {
        var result = Analyze("200.87.1.0/24", new[] { "ana", "bob" },
            Login("ana", "200.87.1.10", 0), Submit("ana", "200.87.1.10", 30),
            Login("bob", "200.87.1.10", 1), Submit("bob", "200.87.1.20", 40));

        Assert.Empty(result.Alerts);
        Assert.True(result.Participants.First().Ips.All(usage => usage.IsLab));
        Assert.Equal(new[] { "200.87.1.0/24" }, result.LabIps);
    }

    [Fact]
    public void Lab_ActivityFromOutsideIsFlaggedWithoutMultipleIpsNoise()
    {
        var result = Analyze("200.87.1.10", new[] { "ana" }, Login("ana", "200.87.1.10", 0), Submit("ana", "181.1.1.1", 50));

        var alert = Assert.Single(result.Alerts);
        Assert.Equal(ExamAlertCodes.OutsideLab, alert.Code);
        Assert.Equal(new[] { "181.1.1.1" }, alert.Ips);
    }

    [Fact]
    public void Lab_OutsideAndBackToLabIsAlsoConcurrentUse()
    {
        var result = Analyze("200.87.1.0/24", new[] { "ana" },
            Login("ana", "200.87.1.10", 0), Submit("ana", "181.1.1.1", 20), Submit("ana", "200.87.1.11", 30));

        Assert.Equal(new[] { ExamAlertCodes.ConcurrentUse, ExamAlertCodes.OutsideLab }, Codes(result, "ana"));
    }

    [Fact]
    public void SameNonLabIpUsedByTwoAccountsIsShared()
    {
        var result = Analyze("200.87.1.0/24", new[] { "ana", "bob", "carl" },
            Login("ana", "181.1.1.1", 0), Login("bob", "181.1.1.1", 5), Login("carl", "200.87.1.5", 5), Login("carl", "181.1.1.9", 9));

        var shared = result.Alerts.Single(alert => alert.Code == ExamAlertCodes.SharedIp);
        Assert.Equal("medium", shared.Severity);
        Assert.Equal(new[] { "ana", "bob" }, shared.UserIds);
        Assert.Contains(ExamAlertCodes.SharedIp, Codes(result, "bob"));
    }

    [Fact]
    public void DockerProxyIpsOnlyRaiseCaptureWarning()
    {
        var result = Analyze(null, new[] { "ana", "bob" },
            Login("ana", "172.18.0.2", 0), Submit("ana", "172.18.0.2", 10), Submit("bob", "172.18.0.2", 20), Submit("bob", "181.1.1.1", 30));

        var alert = Assert.Single(result.Alerts);
        Assert.Equal("IP_CAPTURE", alert.Code);
        Assert.Equal(new[] { "172.18.0.2" }, alert.Ips);
        Assert.Equal(2, result.Participants.Single(participant => participant.UserId == "bob").Ips.Count);
    }

    [Fact]
    public void UnknownOrEmptyIpsAreIgnored()
    {
        var result = Analyze(null, new[] { "ana" }, Login("ana", "", 0), Login("ana", "unknown", 1), Submit("ana", "181.1.1.1", 2));

        Assert.Empty(result.Alerts);
        Assert.Equal("181.1.1.1", result.Participants.Single().Ips.Single().Ip);
    }

    [Fact]
    public void ParticipantsIncludeInactiveUsersAndSubmittersWithUsageSummary()
    {
        var activity = new ExamActivity
        {
            ParticipantUserIds = new[] { "idle", "ana" },
            Nicks = new Dictionary<string, string> { ["ana"] = "Ana", ["idle"] = "" },
            Events = new[] { Login("ana", "181.1.1.1", -5), Submit("ana", "181.1.1.1", 10), Submit("ana", "181.1.1.1", 70), Submit("ghost", "190.1.1.1", 5), Submit("ghost", "181.9.9.9", 6) }
        };

        var result = ExamActivityAnalyzer.Analyze(Exam(), activity, Start);

        Assert.Equal(new[] { "ghost", "ana", "idle" }, result.Participants.Select(participant => participant.UserId));
        var ana = result.Participants.Single(participant => participant.UserId == "ana");
        Assert.Equal("Ana", ana.Nick);
        var usage = ana.Ips.Single();
        Assert.Equal(1, usage.Logins);
        Assert.Equal(2, usage.Submissions);
        Assert.Equal(Start.AddMinutes(-5), usage.FirstSeen);
        Assert.Equal(Start.AddMinutes(70), usage.LastSeen);
        Assert.Equal("idle", result.Participants.Single(participant => participant.UserId == "idle").Nick);
        Assert.Equal("Parcial", result.Title);
    }
}
