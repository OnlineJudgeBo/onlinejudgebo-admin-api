using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Application.Services.Helpers;

public class SiteEmailSettingsTests
{
    private static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Smtp:Host"] = "smtp.global",
            ["Email:Smtp:From:Email"] = "no-reply@global.bo",
            ["Email:Sites:2:Smtp:From:Email"] = "no-reply@site2.bo",
            ["Email:Sites:2:AppName"] = "Sitio Dos",
            ["PasswordRecovery:SmtpPort"] = "587",
        })
        .Build();

    [Theory]
    [InlineData(1, "Smtp:From:Email", "no-reply@global.bo")]
    [InlineData(2, "Smtp:From:Email", "no-reply@site2.bo")]
    [InlineData(2, "AppName", "Sitio Dos")]
    [InlineData(1, "AppName", null)]
    [InlineData(2, "Smtp:Host", "smtp.global")]
    public void Get_PrefersSiteOverrideThenGlobal(int siteId, string key, string? expected)
    {
        Assert.Equal(expected, SiteEmailSettings.Get(Configuration, siteId, key));
    }

    [Fact]
    public void Get_FallsBackToLegacyKeys()
    {
        Assert.Equal("587", SiteEmailSettings.Get(Configuration, 2, "Smtp:Port", "PasswordRecovery:SmtpPort"));
    }
}
