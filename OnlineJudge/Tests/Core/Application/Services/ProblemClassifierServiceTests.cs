using Microsoft.Extensions.Logging.Abstractions;
using OnlineJudgeAdmin.Core.Application.Services.Implementations;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Models;

public class ProblemClassifierServiceTests
{
    private readonly Mock<ITopicRepository> _topics = new();
    private readonly Mock<IChatCompletionClient> _chat = new();

    private Task<ProblemClassificationSuggestion> Suggest() =>
        new ProblemClassifierService(_topics.Object, NullLogger<ProblemClassifierService>.Instance, _chat.Object)
            .SuggestClassificationsAsync(new Problem { Title = "Sum of two numbers" });

    private void Answer(ChatCompletion completion)
    {
        _chat.Setup(item => item.IsConfigured).Returns(true);
        _topics.Setup(item => item.GetAllTopicsAsync()).ReturnsAsync(new List<Topic>
        {
            new()
            {
                TopicId = 1,
                Name = "Matemáticas",
                Classifications = new List<Classification>
                {
                    new() { ClassificationId = 10, Name = "Aritmética básica" },
                    new() { ClassificationId = 11, Name = "Primos" },
                    new() { ClassificationId = 12, Name = "Combinatoria" },
                },
            },
        });
        _chat.Setup(item => item.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), "problem_classifications", It.IsAny<object>()))
            .ReturnsAsync(completion);
    }

    [Fact]
    public async Task SuggestClassificationsAsync_WithoutApiKey_ReturnsUnavailable()
    {
        var suggestion = await Suggest();

        Assert.False(suggestion.Available);
        Assert.Empty(suggestion.Classifications);
        _topics.Verify(r => r.GetAllTopicsAsync(), Times.Never);
    }

    [Fact]
    public async Task SuggestClassificationsAsync_WithNoClassificationsRegistered_ReturnsUnavailable()
    {
        _chat.Setup(item => item.IsConfigured).Returns(true);
        _topics.Setup(r => r.GetAllTopicsAsync()).ReturnsAsync(new List<Topic>());

        var suggestion = await Suggest();

        Assert.False(suggestion.Available);
        Assert.Empty(suggestion.Classifications);
    }

    [Fact]
    public async Task SuggestClassificationsAsync_KeepsTheFirstTwoKnownClassifications()
    {
        Answer(new ChatCompletion(200, """{"classifications":[{"classificationId":11,"reason":"primos"},{"classificationId":99,"reason":"no existe"},{"classificationId":10,"reason":"suma"},{"classificationId":12,"reason":"sobra"}]}""", "m", "id"));

        var suggestion = await Suggest();

        Assert.True(suggestion.Available);
        Assert.Equal(new[] { 11, 10 }, suggestion.Classifications.Select(item => item.ClassificationId));
        Assert.Equal("Matemáticas", suggestion.Classifications.First().Topic!.Name);
        Assert.Equal("suma", suggestion.Reasons[10]);
    }

    [Theory]
    [InlineData(500, null)]
    [InlineData(200, null)]
    [InlineData(200, "{}")]
    [InlineData(200, "no es json")]
    public async Task SuggestClassificationsAsync_WithABadAnswer_ReturnsUnavailable(int status, string? content)
    {
        Answer(new ChatCompletion(status, content, "m", "id"));

        var suggestion = await Suggest();

        Assert.False(suggestion.Available);
    }
}
