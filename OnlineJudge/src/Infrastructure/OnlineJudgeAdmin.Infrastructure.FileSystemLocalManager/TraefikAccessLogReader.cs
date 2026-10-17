using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Infrastructure.FileSystemLocalManager;

// Reads the end of Traefik's JSON access log (BlockRules:AccessLog). Without the file there are no requests.
public class TraefikAccessLogReader : IAccessLogReader
{
    private const int TailBytes = 4 * 1024 * 1024;
    private readonly string? _path;

    public TraefikAccessLogReader(IConfiguration configuration)
    {
        _path = configuration["BlockRules:AccessLog"];
    }

    public async Task<IReadOnlyList<AccessLogEntry>> ReadLastAsync(int max)
    {
        if (string.IsNullOrWhiteSpace(_path) || !File.Exists(_path))
        {
            return [];
        }

        string tail;
        await using (var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            stream.Seek(Math.Max(0, stream.Length - TailBytes), SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            tail = await reader.ReadToEndAsync();
        }

        var entries = new List<AccessLogEntry>();
        var lines = tail.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // The first line may be cut by the seek; Parse skips it like any other broken line.
        for (var i = lines.Length - 1; i >= 0 && entries.Count < max; i--)
        {
            if (Parse(lines[i]) is { } entry)
            {
                entries.Add(entry);
            }
        }
        return entries;
    }

    public static AccessLogEntry? Parse(string line)
    {
        try
        {
            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;
            string? Text(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            double Number(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : 0;

            return new AccessLogEntry
            {
                Time = DateTime.TryParse(Text("time") ?? Text("StartUTC"), out var time) ? time : DateTime.MinValue,
                Ip = Text("ClientHost") ?? string.Empty,
                Method = Text("RequestMethod") ?? string.Empty,
                Url = Text("RequestPath") ?? string.Empty,
                Status = (int)Number("DownstreamStatus"),
                DurationMs = Math.Round(Number("Duration") / 1_000_000, 1), // Traefik logs nanoseconds
                UserAgent = Text("request_User-Agent"),
                Referer = Text("request_Referer"),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
