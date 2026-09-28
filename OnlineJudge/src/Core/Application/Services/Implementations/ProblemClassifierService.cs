using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net.Http.Headers;
using System.Text;
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
    private const string DefaultModel = "google/gemini-3.1-flash-lite";
    private const int MaxSuggestions = 2;
    private static readonly HttpClient HttpClient = new();

    private readonly ITopicRepository _topicRepository;

    public ProblemClassifierService(ITopicRepository topicRepository)
    {
        _topicRepository = topicRepository ?? throw new ArgumentNullException(nameof(topicRepository));
    }

    public static bool IsConfigured =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"));

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
            var optionsList = string.Join(
                "\n",
                options.Select(o => $"- id={o.classification.ClassificationId}: {o.Name} > {o.classification.Name}"));

            var schema = new Dictionary<string, JsonElement>
            {
                ["type"] = JsonSerializer.SerializeToElement("object"),
                ["properties"] = JsonSerializer.SerializeToElement(new
                {
                    classifications = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                classificationId = new { type = "integer", @enum = options.Select(o => o.classification.ClassificationId).ToArray() },
                                reason = new { type = "string" },
                            },
                            required = new[] { "classificationId", "reason" },
                            additionalProperties = false,
                        },
                    },
                }),
                ["required"] = JsonSerializer.SerializeToElement(new[] { "classifications" }),
                ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
            };

            var systemPrompt = $"""
                Eres un clasificador de problemas de programación competitiva. Recibes el
                enunciado de un problema y la lista completa de clasificaciones que ya
                existen en el sistema, cada una con su id. Elige únicamente
                clasificaciones de esa lista que apliquen al problema; nunca inventes una
                clasificación ni un id que no esté en la lista. Si ninguna aplica bien,
                devuelve una lista vacía en vez de forzar una que no encaje.

                Reglas para ser consistente:
                - Clasifica según la solución más directa, la que haría un estudiante
                  promedio, no según soluciones alternativas o más elaboradas.
                - Devuelve una sola clasificación. Agrega una segunda solo si esa solución
                  necesita dos técnicas distintas y las dos son imprescindibles. Nunca más de 2.
                - Ordénalas de la más importante a la menos importante.
                - No elijas técnicas avanzadas (máscaras de bits, FFT, estructuras de datos
                  avanzadas, programación dinámica compleja…) salvo que el problema no se pueda
                  resolver sin ellas. Si el problema trata de cadenas y se resuelve
                  recorriéndolas, clasifícalo en el tema de cadenas.
                - Prefiere la clasificación más específica. No agregues una general del mismo
                  tema (por ejemplo "Matemáticas") si ya elegiste una más concreta
                  (por ejemplo "Aritmética básica").
                - Usa "Ad hoc" solo si el problema no requiere ninguna técnica concreta de la lista.
                - Para cada una escribe en "reason" una frase corta en español que diga qué
                  parte del problema la justifica.

                Clasificaciones disponibles:
                {optionsList}
                """;

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"));
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                model = Environment.GetEnvironmentVariable("OPENROUTER_CLASSIFIER_MODEL") ?? DefaultModel,
                max_tokens = 1024,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = BuildStatementText(problem) }
                },
                response_format = new
                {
                    type = "json_schema",
                    json_schema = new { name = "problem_classifications", strict = true, schema }
                }
            }), Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await HttpClient.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            using var completion = JsonDocument.Parse(responseBody);
            var json = completion.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            if (json is null)
            {
                return Unavailable("No se pudo generar una sugerencia en este momento.");
            }

            using var parsed = JsonDocument.Parse(json);
            // Kept in the model's order (most important first) and capped, in case it returns more.
            var reasons = new Dictionary<int, string>();
            foreach (var item in parsed.RootElement.GetProperty("classifications").EnumerateArray())
            {
                var id = item.GetProperty("classificationId").GetInt32();
                if (reasons.Count < MaxSuggestions && !reasons.ContainsKey(id))
                {
                    reasons[id] = item.GetProperty("reason").GetString() ?? string.Empty;
                }
            }

            return new ProblemClassificationSuggestion
            {
                Available = true,
                // GetAllTopicsAsync's projection doesn't populate Classification.Topic (it
                // only needs the id/name for the prompt) -- fill it in here so a caller
                // formatting a "<Topic> > <Classification>" label, or merging this into
                // the existing Topic/Classification picker, doesn't need a second lookup.
                Classifications = reasons.Keys
                    .Select(id => options.First(o => o.classification.ClassificationId == id))
                    .Select(o =>
                    {
                        o.classification.Topic ??= new Topic { TopicId = o.TopicId, Name = o.Name };
                        return o.classification;
                    })
                    .ToList(),
                Reasons = reasons,
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

    // Samples and hints often give away the technique, so they go in too.
    private static string BuildStatementText(Problem problem)
    {
        var samples = problem.SampleCases.Count > 0
            ? problem.SampleCases.OrderBy(sample => sample.Num).Select(sample => (sample.Input, sample.Output)).ToList()
            : new List<(string?, string?)> { (problem.SampleInput, problem.SampleOutput) };
        var sampleText = string.Join("\n", samples
            .Where(sample => !string.IsNullOrWhiteSpace(sample.Item1) || !string.IsNullOrWhiteSpace(sample.Item2))
            .Select((sample, index) => $"Ejemplo {index + 1} - entrada:\n{sample.Item1}\nEjemplo {index + 1} - salida:\n{sample.Item2}"));
        return $"""
            Título: {problem.Title}
            Descripción: {StripHtml(problem.Description)}
            Entrada: {StripHtml(problem.Input)}
            Salida: {StripHtml(problem.Output)}
            Notas: {StripHtml(problem.Hint)}
            {sampleText}
            """;
    }

    private static string StripHtml(string? html) =>
        string.IsNullOrEmpty(html) ? string.Empty : Regex.Replace(html, "<[^>]+>", " ");
}
