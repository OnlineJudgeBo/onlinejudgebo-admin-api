using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Infrastructure.FileSystemLocalManager;

// Writes groups into the control-server's groups.json (re-read on every request, so no restart).
// The file lives in a directory shared with the control-server container; writes are atomic renames.
public class ControlGroupFileStore : IControlGroupStore
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    private readonly IConfiguration _configuration;

    public ControlGroupFileStore(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task EnsureAsync(ControlGroup group)
    {
        var path = _configuration["ControlServer:GroupsFile"];
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("ControlServer:GroupsFile is not configured.");
        }

        await WriteLock.WaitAsync();
        try
        {
            var groups = File.Exists(path)
                ? JsonNode.Parse(await File.ReadAllTextAsync(path)) as JsonObject ?? new JsonObject()
                : new JsonObject();
            var before = groups.ToJsonString();

            groups[group.Id] = new JsonObject
            {
                ["enroll_token"] = group.EnrollToken,
                ["admin_token"] = group.AdminToken,
                ["label"] = group.Label,
            };
            // Machines boot into "lobby" with the enroll token baked into the ISO, before anyone logs in.
            var lobbyToken = _configuration["ControlServer:LobbyEnrollToken"];
            if (!string.IsNullOrWhiteSpace(lobbyToken))
            {
                groups["lobby"] = new JsonObject { ["enroll_token"] = lobbyToken, ["label"] = "Sin examen" };
            }

            if (groups.ToJsonString() == before)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temp = path + ".tmp";
            await File.WriteAllTextAsync(temp, groups.ToJsonString(Indented));
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            WriteLock.Release();
        }
    }
}
