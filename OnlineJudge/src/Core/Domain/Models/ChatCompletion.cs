namespace OnlineJudgeAdmin.Core.Domain.Models;

// Content is null when the provider answered without a usable message.
public record ChatCompletion(int StatusCode, string? Content, string? Model, string? RequestId)
{
    public bool Success => StatusCode is >= 200 and < 300;
}
