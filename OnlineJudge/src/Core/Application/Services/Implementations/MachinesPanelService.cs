using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Infrastructure;
using OnlineJudgeAdmin.Core.Domain.Abstractions.Services;
using OnlineJudgeAdmin.Core.Domain.Models;

namespace OnlineJudgeAdmin.Core.Application.Services.Implementations;

// Machines panel of an exam: calls the control-server's /admin/* with the exam group's own
// admin token, so the control-server itself limits the call to that contest's machines. The few
// superadmin-only operations use the superadmin token and are pinned to the group here.
public class MachinesPanelService(IContestMachinesService machinesService, IControlServerClient controlServer, IConfiguration configuration) : IMachinesPanelService
{
    // Only what MachinesPage uses. New control-server admin endpoints stay private by default.
    private static readonly HashSet<string> AllowedSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "alerts", "allowlist", "cmd", "commands", "homepage", "homes", "logo", "machines", "report",
    };

    // Mirror of SUPERADMIN_ONLY in control-server/control/settings.py.
    private static readonly HashSet<string> SuperadminActions = new() { "unlock-root", "lock-root", "net-open", "usb-block", "usb-unblock", "collect-home", "set-allowlist" };

    public async Task<MachinesForwardResult> ForwardAsync(int contestId, int siteId, string method, string? path, string query, string? body, CancellationToken cancellationToken = default)
    {
        var segments = (path ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || !AllowedSections.Contains(segments[0]) || segments.Any(segment => segment is "." or ".."))
        {
            return new MachinesForwardResult(404);
        }

        var group = await machinesService.GetGroupAsync(contestId, siteId);
        if (!controlServer.IsConfigured)
        {
            return new MachinesForwardResult(503, "El control de máquinas no está configurado.");
        }

        var token = group.AdminToken;

        // The control-server only allows these to its superadmin token, which is not limited to one group,
        // so here Patito pins them to this exam's group itself.
        var superadmin = segments[0] == "allowlist"
            || (segments[0] == "cmd" && SuperadminActions.Contains(JsonField(body, "action") ?? string.Empty));
        if (superadmin)
        {
            token = configuration["ControlServer:AdminToken"] ?? string.Empty;
            if (token.Length == 0)
            {
                return new MachinesForwardResult(503, "Esta acción requiere ControlServer:AdminToken.");
            }

            if (segments[0] == "allowlist")
            {
                query = $"?group={Uri.EscapeDataString(group.Id)}";
                body = body == null ? null : WithGroup(body, group.Id);
            }
            else if (!await TargetsOnlyGroupAsync(group, body!))
            {
                return new MachinesForwardResult(403, "Solo se pueden enviar órdenes a las máquinas de este examen.");
            }
        }

        var response = await controlServer.SendAsync(method, $"admin/{string.Join('/', segments)}{query}", token, body, cancellationToken);
        return response == null
            ? new MachinesForwardResult(502, "No se pudo conectar con el control de máquinas.")
            : new MachinesForwardResult(response.StatusCode, Response: response);
    }

    // onlyIfSet: the control-server answers its own default homepage (updated_at null) when nobody set one.
    public async Task<string?> GetGroupSettingAsync(ControlGroup group, string setting, string field, bool onlyIfSet = false)
    {
        if (!controlServer.IsConfigured)
        {
            return null;
        }

        try
        {
            using var response = await controlServer.SendAsync("GET", $"admin/{setting}?group={Uri.EscapeDataString(group.Id)}", group.AdminToken);
            if (response is not { Success: true })
            {
                return null;
            }

            using var json = await JsonDocument.ParseAsync(response.Content);
            if (onlyIfSet && (!json.RootElement.TryGetProperty("updated_at", out var updatedAt) || updatedAt.ValueKind == JsonValueKind.Null))
            {
                return null;
            }

            var value = json.RootElement.TryGetProperty(field, out var property) ? property.GetString() : null;
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (Exception ex) when (ex is TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    // A superadmin command may target this exam's group or its machines, never "all" or another group.
    private async Task<bool> TargetsOnlyGroupAsync(ControlGroup group, string body)
    {
        if (JsonNode.Parse(body)?["target"] is not JsonObject target || target.ContainsKey("all"))
        {
            return false;
        }

        var groupId = target["group_id"]?.GetValue<string>();
        var machineId = target["machine_id"]?.GetValue<string>();
        if (groupId == null && machineId == null)
        {
            return false;
        }

        if (groupId != null && groupId != group.Id)
        {
            return false;
        }

        // "*" with the group means every machine of the group.
        if (machineId == null || (machineId == "*" && groupId != null))
        {
            return true;
        }

        using var response = await controlServer.SendAsync("GET", "admin/machines", group.AdminToken);
        if (response is not { Success: true })
        {
            return false;
        }

        var machines = (await JsonNode.ParseAsync(response.Content))?["machines"]?.AsArray() ?? new JsonArray();
        return machines.Any(machine => machine?["machine_id"]?.GetValue<string>() == machineId);
    }

    private static string? JsonField(string? body, string field)
    {
        try
        {
            return body == null ? null : (JsonNode.Parse(body) as JsonObject)?[field]?.GetValue<string>();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string WithGroup(string body, string groupId)
    {
        var json = JsonNode.Parse(body) as JsonObject ?? new JsonObject();
        json["group_id"] = groupId;
        return json.ToJsonString();
    }
}
