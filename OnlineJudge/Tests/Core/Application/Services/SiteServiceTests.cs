using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;

public class SiteServiceTests
{
    private static (SiteService Service, Mock<ISiteRepository> Repository) Create(string? domainOfOtherSite = null)
    {
        var repository = new Mock<ISiteRepository>();
        repository.Setup(item => item.GetAsync(3)).ReturnsAsync(new SiteSettings { SiteId = 3, Name = "Juez" });
        repository.Setup(item => item.FindDomainOfOtherSiteAsync(3, It.IsAny<IEnumerable<string>>())).ReturnsAsync(domainOfOtherSite);
        return (new SiteService(repository.Object), repository);
    }

    [Fact]
    public async Task UpdateAsync_NormalizesAndSaves()
    {
        var (service, repository) = Create();

        var saved = await service.UpdateAsync(new SiteSettings
        {
            SiteId = 3,
            Name = "  Colegio X ",
            LogoUrl = " ",
            PrimaryColor = "#1E88E5",
            Domains = ["Juez.Colegio.bo:443", "juez.colegio.bo", "www.juez.colegio.bo.", ""]
        });

        Assert.Equal("Colegio X", saved.Name);
        Assert.Null(saved.LogoUrl);
        Assert.Equal("#1e88e5", saved.PrimaryColor);
        Assert.Equal(["juez.colegio.bo", "www.juez.colegio.bo"], saved.Domains);
        repository.Verify(item => item.UpdateAsync(saved), Times.Once);
    }

    [Theory]
    [InlineData("", null, null, "a.bo")]
    [InlineData("X", "javascript:alert(1)", null, "a.bo")]
    [InlineData("X", null, "blue", "a.bo")]
    [InlineData("X", null, null, "bad domain")]
    [InlineData("X", null, null, "-a.bo")]
    public async Task UpdateAsync_RejectsInvalidValues(string name, string? logo, string? color, string domain)
    {
        var (service, repository) = Create();

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(new SiteSettings { SiteId = 3, Name = name, LogoUrl = logo, PrimaryColor = color, Domains = [domain] }));
        repository.Verify(item => item.UpdateAsync(It.IsAny<SiteSettings>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_RejectsADomainOfAnotherSite()
    {
        var (service, _) = Create(domainOfOtherSite: "jv.umsa.bo");

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(new SiteSettings { SiteId = 3, Name = "X", Domains = ["jv.umsa.bo"] }));

        Assert.Contains("jv.umsa.bo", error.Message);
    }
}
