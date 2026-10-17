using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
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
        var service = new BlockRuleService(new Mock<IBlockRuleRepository>().Object);
        await Assert.ThrowsAsync<ArgumentException>(() => service.AddAsync(type, value, null));
    }
}
