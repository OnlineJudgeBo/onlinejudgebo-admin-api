using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Infrastructure.Http;

public class OpenRouterChatClient : IChatCompletionClient
{
    private static readonly HttpClient HttpClient = new();

    public bool IsConfigured =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"));

    public async Task<ChatCompletion> CompleteJsonAsync(string model, string systemPrompt, string userText, string schemaName, object schema)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"));
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model,
            max_tokens = 1024,
            provider = new { require_parameters = true },
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userText }
            },
            response_format = new
            {
                type = "json_schema",
                json_schema = new { name = schemaName, strict = true, schema }
            }
        }), Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await HttpClient.SendAsync(request);
        string responseBody = await response.Content.ReadAsStringAsync();
        var requestId = response.Headers.TryGetValues("x-openrouter-request-id", out var requestIds)
            ? requestIds.FirstOrDefault()
            : null;
        if (!response.IsSuccessStatusCode)
        {
            return new ChatCompletion((int)response.StatusCode, null, model, requestId);
        }

        using var completion = JsonDocument.Parse(responseBody);
        var completionId = completion.RootElement.TryGetProperty("id", out var idElement) ? idElement.GetString() : requestId;
        var responseModel = completion.RootElement.TryGetProperty("model", out var modelElement) ? modelElement.GetString() : model;
        var content = completion.RootElement.TryGetProperty("choices", out var choices)
            && choices.ValueKind == JsonValueKind.Array
            && choices.GetArrayLength() > 0
            && choices[0].ValueKind == JsonValueKind.Object
            && choices[0].TryGetProperty("message", out var message)
            && message.ValueKind == JsonValueKind.Object
            && message.TryGetProperty("content", out var contentElement)
            && contentElement.ValueKind == JsonValueKind.String
                ? contentElement.GetString()
                : null;
        return new ChatCompletion((int)response.StatusCode, content, responseModel, completionId);
    }
}
