using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;

public class ProblemClassifierServiceTests
{
    // Never calls the Anthropic API in this suite -- both cases here short-circuit
    // before any network call, matching how BocaImportControllerTests only exercises
    // the guard rails rather than the live PDF transcription.
    [Fact]
    public async Task SuggestClassificationsAsync_WithoutApiKey_ReturnsUnavailable()
    {
        var topicRepository = new Mock<ITopicRepository>();
        var service = new ProblemClassifierService(topicRepository.Object);

        var original = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);

            var suggestion = await service.SuggestClassificationsAsync(new Problem { Title = "Sum of two numbers" });

            Assert.False(suggestion.Available);
            Assert.Empty(suggestion.Classifications);
            topicRepository.Verify(r => r.GetAllTopicsAsync(), Times.Never);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", original);
        }
    }

    [Fact]
    public async Task SuggestClassificationsAsync_WithNoClassificationsRegistered_ReturnsUnavailable()
    {
        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(r => r.GetAllTopicsAsync()).ReturnsAsync(new List<Topic>());
        var service = new ProblemClassifierService(topicRepository.Object);

        var original = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "test-key");

            var suggestion = await service.SuggestClassificationsAsync(new Problem { Title = "Sum of two numbers" });

            Assert.False(suggestion.Available);
            Assert.Empty(suggestion.Classifications);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", original);
        }
    }
}
