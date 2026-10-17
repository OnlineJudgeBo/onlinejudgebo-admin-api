using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Infrastructure.FileSystemLocalManager;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Models;

public class BlockRuleTests
{
    private static readonly BlockRule[] Rules =
    [
        new() { Type = BlockRule.IpType, Value = "200.87.1.5" },
        new() { Type = BlockRule.IpType, Value = "10.20.0.0/16" },
        new() { Type = BlockRule.TextType, Value = "gptbot" },
    ];

    [Theory]
    [InlineData("200.87.1.5", "Mozilla", true)]
    [InlineData("10.20.33.4", "Mozilla", true)]
    [InlineData("10.21.0.1", "Mozilla", false)]
    [InlineData("190.1.1.1", "Mozilla/5.0 (compatible; GPTBot/1.2)", true)]
    [InlineData("190.1.1.1", "Mozilla/5.0", false)]
    public void Blocks_MatchesIpCidrAndTextIgnoringCase(string ip, string userAgent, bool blocked)
    {
        Assert.Equal(blocked, BlockRule.Blocks(Rules, ip, BlockRule.RequestText(userAgent, null, "/api/x")));
    }

    [Theory]
    [InlineData("ip", "no-es-ip")]
    [InlineData("otro", "x")]
    [InlineData("text", " ")]
    public async Task AddAsync_RejectsInvalidRules(string type, string value)
    {
        var service = new BlockRuleService(new Mock<IBlockRuleRepository>().Object, new Mock<IAccessLogReader>().Object, new Mock<IIpCountryLookup>().Object);
        await Assert.ThrowsAsync<ArgumentException>(() => service.AddAsync(type, value, null));
    }

    [Fact]
    public async Task RecentRequests_ReadsTraefikJsonNewestFirstAndMarksBlocked()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllLinesAsync(path,
        [
            "cut line from the seek",
            """{"ClientHost":"190.1.1.1","RequestMethod":"GET","RequestPath":"/oj/status.php","DownstreamStatus":200,"Duration":7800000000,"request_User-Agent":"Mozilla/5.0","time":"2026-10-16T18:00:00-04:00"}""",
            """{"ClientHost":"10.0.0.1","RequestMethod":"GET","RequestPath":"/api/blockrules/requests?limit=300","DownstreamStatus":200,"Duration":1000000,"time":"2026-10-16T18:00:02-04:00"}""",
            """{"ClientHost":"200.87.1.5","RequestMethod":"GET","RequestPath":"/oj/index.php","DownstreamStatus":200,"Duration":300000000,"request_User-Agent":"GPTBot/1.2","request_Referer":"https://chatgpt.com/","time":"2026-10-16T18:00:01-04:00"}""",
        ]);
        var reader = new TraefikAccessLogReader(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BlockRules:AccessLog"] = path }).Build());
        var repository = new Mock<IBlockRuleRepository>();
        repository.Setup(r => r.GetAllAsync()).ReturnsAsync(Rules);
        var countries = new Mock<IIpCountryLookup>();
        countries.Setup(c => c.CountryOf("200.87.1.5")).Returns("BO");
        var service = new BlockRuleService(repository.Object, reader, countries.Object);

        var all = await service.GetRecentRequestsAsync(null, null, 10);
        Assert.Equal(["200.87.1.5", "190.1.1.1"], all.Select(e => e.Ip));
        Assert.True(all[0].Blocked);
        Assert.False(all[1].Blocked);
        Assert.Equal(7800, all[1].DurationMs);
        Assert.Equal("https://chatgpt.com/", all[0].Referer);
        Assert.Equal("BO", all[0].Country);
        Assert.Null(all[1].Country);

        var search = await service.GetRecentRequestsAsync(null, "chatgpt", 10);
        Assert.Equal("200.87.1.5", Assert.Single(search).Ip);
        File.Delete(path);
    }
}

public class DbIpCountryLookupTests
{
    [Fact]
    public void WithoutTheDatabase_EveryCountryIsUnknown()
    {
        using var geo = new OnlineJudgeAdmin.Infrastructure.FileSystemLocalManager.DbIpCountryLookup(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BlockRules:CountryDb"] = "/no/such/file.mmdb" }).Build());
        Assert.Null(geo.CountryOf("8.8.8.8"));
    }
}
