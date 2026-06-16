using OnlineJudgeAdmin.Core.Application.Services.Implementations.IdeIntegration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

public class IdeLanguageDefinitionServiceTests
{
    [Fact]
    public async Task GetAllowedLanguageDefinitionsAsync_ReturnsAllLanguagesWhenTokenDoesNotRestrictThem()
    {
        var programmingLanguageService = CreateProgrammingLanguageService(new[]
        {
            new ProgrammingLanguage { LanguageId = 1, Name = "GNU C++17" },
            new ProgrammingLanguage { LanguageId = 2, Name = "Python 3" },
            new ProgrammingLanguage { LanguageId = null, Name = "Broken" }
        });
        var service = new IdeLanguageDefinitionService(programmingLanguageService.Object);

        var languages = await service.GetAllowedLanguageDefinitionsAsync(Array.Empty<int>());

        Assert.Collection(languages,
            language =>
            {
                Assert.Equal(1, language.JudgeLanguageId);
                Assert.Equal("GNU C++17", language.Name);
                Assert.Equal("cpp", language.IdeLanguage);
            },
            language =>
            {
                Assert.Equal(2, language.JudgeLanguageId);
                Assert.Equal("Python 3", language.Name);
                Assert.Equal("python", language.IdeLanguage);
            });
    }

    [Fact]
    public async Task GetAllowedLanguageDefinitionsAsync_FiltersByAllowedLanguageIds()
    {
        var programmingLanguageService = CreateProgrammingLanguageService(new[]
        {
            new ProgrammingLanguage { LanguageId = 1, Name = "Java 17" },
            new ProgrammingLanguage { LanguageId = 2, Name = "Node.js" },
            new ProgrammingLanguage { LanguageId = 3, Name = "Rust" }
        });
        var service = new IdeLanguageDefinitionService(programmingLanguageService.Object);

        var languages = await service.GetAllowedLanguageDefinitionsAsync(new[] { 2 });

        var language = Assert.Single(languages);
        Assert.Equal(2, language.JudgeLanguageId);
        Assert.Equal("Node.js", language.Name);
        Assert.Equal("javascript", language.IdeLanguage);
    }

    [Theory]
    [InlineData("Java 21", "java")]
    [InlineData("Go", "go")]
    [InlineData("Golang", "go")]
    [InlineData("Rust 1.75", "rust")]
    [InlineData("Pascal/FPC", "pascal-fpc")]
    [InlineData(null, "text")]
    public async Task GetAllowedLanguageDefinitionsAsync_NormalizesLanguageNames(string? name, string expectedIdeLanguage)
    {
        var programmingLanguageService = CreateProgrammingLanguageService(new[]
        {
            new ProgrammingLanguage { LanguageId = 1, Name = name }
        });
        var service = new IdeLanguageDefinitionService(programmingLanguageService.Object);

        var language = Assert.Single(await service.GetAllowedLanguageDefinitionsAsync(Array.Empty<int>()));

        Assert.Equal(expectedIdeLanguage, language.IdeLanguage);
        Assert.Equal(name ?? string.Empty, language.Name);
    }

    private static Mock<IProgrammingLanguageService> CreateProgrammingLanguageService(IEnumerable<ProgrammingLanguage> languages)
    {
        var programmingLanguageService = new Mock<IProgrammingLanguageService>();
        programmingLanguageService
            .Setup(item => item.GetAllProgrammingLanguageAsync())
            .ReturnsAsync(languages);

        return programmingLanguageService;
    }
}
