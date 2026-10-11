using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;

public interface IChatCompletionClient
{
    bool IsConfigured { get; }

    // Asks the model for a JSON answer that follows the given JSON schema.
    Task<ChatCompletion> CompleteJsonAsync(string model, string systemPrompt, string userText, string schemaName, object schema);
}
