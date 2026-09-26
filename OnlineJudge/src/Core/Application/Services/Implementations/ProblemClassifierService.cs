using System.Text.Json;
using System.Text.RegularExpressions;
using Anthropic;
using Anthropic.Models.Messages;
using AnthropicRole = Anthropic.Models.Messages.Role;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Repositories;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

// Suggests EXISTING classifications for a problem -- never invents a new topic or
// classification. The prompt gives the model the full, current Topic/Classification
// list from ITopicRepository and constrains the response to an enum of those exact
// ids, so a hallucinated id is a JSON Schema violation rather than a silent
// miscategorization. Never persists anything: whoever calls this (the BOCA import
// preview, or an admin reviewing an already-created problem) decides which suggestions
// to keep, through the same Problem.Classifications field every other problem write
// already goes through (see ProblemService.CreateProblemAsync/UpdateProblemAsync).
public class ProblemClassifierService : IProblemClassifierService
{
    // Picking among a fixed, known list is plain classification, not the kind of task
    // that needs Opus-tier reasoning -- see the PDF statement transcription instead for
    // where that tier earns its cost.
    private const string Model = "claude-sonnet-5";

    private readonly ITopicRepository _topicRepository;

    public ProblemClassifierService(ITopicRepository topicRepository)
    {
        _topicRepository = topicRepository ?? throw new ArgumentNullException(nameof(topicRepository));
    }

    public static bool IsConfigured =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));

    public async Task<ProblemClassificationSuggestion> SuggestClassificationsAsync(Problem problem)
    {
        if (!IsConfigured)
        {
            // Deliberately doesn't name the env var or the provider here: this string
            // reaches the admin verbatim as a toast (see EditProblemPage.jsx), and the
            // whole point of calling this "clasificación automática" in the UI is to not
            // advertise which LLM (or that one at all) is behind it.
            return Unavailable("La clasificación automática no está disponible en este momento.");
        }

        var topics = await _topicRepository.GetAllTopicsAsync();
        var options = topics
            .SelectMany(topic => topic.Classifications.Select(classification => (topic.TopicId, topic.Name, classification)))
            .ToList();
        if (options.Count == 0)
        {
            return Unavailable("No hay clasificaciones registradas todavía.");
        }

        try
        {
            AnthropicClient client = new();

            var optionsList = string.Join(
                "\n",
                options.Select(o => $"- id={o.classification.ClassificationId}: {o.Name} > {o.classification.Name}"));

            var schema = new Dictionary<string, JsonElement>
            {
                ["type"] = JsonSerializer.SerializeToElement("object"),
                ["properties"] = JsonSerializer.SerializeToElement(new
                {
                    classificationIds = new
                    {
                        type = "array",
                        items = new { type = "integer", @enum = options.Select(o => o.classification.ClassificationId).ToArray() },
                    },
                }),
                ["required"] = JsonSerializer.SerializeToElement(new[] { "classificationIds" }),
                ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
            };

            var response = await client.Messages.Create(new MessageCreateParams
            {
                Model = Model,
                MaxTokens = 1024,
                System = $"""
                    Sos un clasificador de problemas de programación competitiva. Se te da el
                    enunciado de un problema y la lista completa de clasificaciones que ya
                    existen en el sistema, cada una con su id. Elegí únicamente
                    clasificaciones de esa lista que apliquen al problema -- nunca inventes una
                    clasificación ni un id que no esté en la lista. Elegí como mucho 3, las más
                    específicas y relevantes; si ninguna aplica bien, devolvé una lista vacía en
                    vez de forzar una que no calza.

                    Clasificaciones disponibles:
                    {optionsList}
                    """,
                OutputConfig = new OutputConfig { Format = new JsonOutputFormat { Schema = schema } },
                Messages = [new() { Role = AnthropicRole.User, Content = BuildStatementText(problem) }],
            });

            var json = response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text).FirstOrDefault();
            if (json is null)
            {
                return Unavailable("No se pudo generar una sugerencia en este momento.");
            }

            using var parsed = JsonDocument.Parse(json);
            var suggestedIds = parsed.RootElement.GetProperty("classificationIds")
                .EnumerateArray()
                .Select(e => e.GetInt32())
                .ToHashSet();

            return new ProblemClassificationSuggestion
            {
                Available = true,
                // GetAllTopicsAsync's projection doesn't populate Classification.Topic (it
                // only needs the id/name for the prompt) -- fill it in here so a caller
                // formatting a "<Topic> > <Classification>" label, or merging this into
                // the existing Topic/Classification picker, doesn't need a second lookup.
                Classifications = options
                    .Where(o => suggestedIds.Contains(o.classification.ClassificationId))
                    .Select(o =>
                    {
                        o.classification.Topic ??= new Topic { TopicId = o.TopicId, Name = o.Name };
                        return o.classification;
                    })
                    .ToList(),
            };
        }
        catch (Exception error)
        {
            // error.Message can name the provider/SDK in its wording (timeouts, auth
            // failures, etc.) -- logged for whoever runs this, never handed to the
            // Unavailable() string the admin's toast displays verbatim.
            Console.WriteLine($"ProblemClassifierService.SuggestClassificationsAsync failed: {error.Message}");
            return Unavailable("No se pudo generar una sugerencia en este momento.");
        }
    }

    private static ProblemClassificationSuggestion Unavailable(string reason) =>
        new() { Available = false, UnavailableReason = reason };

    private static string BuildStatementText(Problem problem) => $"""
        Título: {problem.Title}
        Descripción: {StripHtml(problem.Description)}
        Entrada: {StripHtml(problem.Input)}
        Salida: {StripHtml(problem.Output)}
        """;

    private static string StripHtml(string? html) =>
        string.IsNullOrEmpty(html) ? string.Empty : Regex.Replace(html, "<[^>]+>", " ");
}
