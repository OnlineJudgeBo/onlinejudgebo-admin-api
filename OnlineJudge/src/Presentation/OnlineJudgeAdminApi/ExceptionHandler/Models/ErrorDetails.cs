namespace OnlineJudgeAdminApi;

using System.Text.Json;

public class ErrorDetails
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public int StatusCode { get; set; }

    public string Message { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, SerializerOptions);
    }
}
