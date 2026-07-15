using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UnityMcp.Bridge;

if (args.Contains("--self-test", StringComparer.Ordinal))
{
    SimulatorState.SelfTest();
    Console.WriteLine("BepMcp.Simulator self-test ok");
    return;
}

var url = Environment.GetEnvironmentVariable("BEPMCP_SIMULATOR_URL") ?? "http://127.0.0.1:8765/";
var address = new Uri(url);
if (address.Scheme != Uri.UriSchemeHttp || !IPAddress.TryParse(address.Host, out var host) ||
    !IPAddress.IsLoopback(host))
{
    throw new InvalidOperationException("Simulator URL must use a loopback HTTP address.");
}

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.UseUrls(url);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1024 * 1024);
var app = builder.Build();
var state = new SimulatorState();

app.MapGet("/healthz", () => Results.Json(new
{
    ok = true,
    processId = Process.GetCurrentProcess().Id,
    simulator = true
}));
app.MapGet("/snapshot", () => Results.Text(
    state.Snapshot(Guid.NewGuid().ToString("N")), "application/json"));
app.MapPost("/act", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    return Results.Text(state.Act(await reader.ReadToEndAsync()), "application/json");
});

await app.RunAsync();

internal sealed class SimulatorState
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly object _sync = new object();
    private float _playerX;
    private float _playerZ;
    private float _timeScale = 1;
    private bool _doorOpen;
    private bool _treasureCollected;

    public string Snapshot(string requestId)
    {
        lock (_sync)
        {
            var entities = new List<object>
            {
                Entity("Player", "World/Player", 1, _playerX, _playerZ, "player"),
                Entity("Door", "World/Door", 2, 3, 0, _doorOpen ? "open" : "closed")
            };
            if (!_treasureCollected)
            {
                entities.Add(Entity("Treasure", "World/Treasure", 3, 5, 0, "collectible"));
            }

            entities.Add(new
            {
                id = StableEntityId.Create("Simulator", "Canvas/Status", 4),
                kind = "ui",
                name = "Status",
                path = "Canvas/Status",
                instanceId = 4,
                active = true,
                ui = new
                {
                    text = _treasureCollected ? "Treasure collected" : "Find the treasure",
                    interactable = false,
                    visible = true,
                    bounds = new { x = 24, y = 24, width = 240, height = 48 },
                    components = new[] { "RectTransform", "Text" }
                },
                actions = Array.Empty<string>()
            });

            return JsonSerializer.Serialize(new
            {
                schema = BridgeSchemas.BridgeV1,
                requestId,
                ok = true,
                result = new
                {
                    runtime = new { unityVersion = "simulator", scriptingBackend = "managed", platform = Environment.OSVersion.Platform.ToString() },
                    game = new { id = "bepmcp-simulator", scene = "Simulator", timeScale = _timeScale },
                    entities,
                    actions = Descriptors()
                },
                error = (object?)null
            }, JsonOptions);
        }
    }

    public string Act(string body)
    {
        string requestId;
        bool dryRun;
        JsonElement actions;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            requestId = root.TryGetProperty("requestId", out var id) ? id.GetString() ?? "unknown" : "unknown";
            var payload = root.GetProperty("payload");
            dryRun = payload.TryGetProperty("dryRun", out var dry) && dry.GetBoolean();
            actions = payload.GetProperty("actions").Clone();
        }
        catch (Exception ex)
        {
            return Error("unknown", "INVALID_JSON", ex.Message);
        }

        if (actions.ValueKind != JsonValueKind.Array || actions.GetArrayLength() is < 1 or > 100)
        {
            return Error(requestId, "INVALID_ACTION", "Payload must contain 1 to 100 actions.");
        }

        lock (_sync)
        {
            var results = new List<object>();
            var index = 0;
            foreach (var action in actions.EnumerateArray())
            {
                results.Add(Execute(action, index++, dryRun));
            }

            return JsonSerializer.Serialize(new
            {
                schema = BridgeSchemas.BridgeV1,
                requestId,
                ok = true,
                result = new { actions = results },
                error = (object?)null
            }, JsonOptions);
        }
    }

    private object Execute(JsonElement action, int index, bool dryRun)
    {
        var verb = action.TryGetProperty("verb", out var verbElement) ? verbElement.GetString() : null;
        var args = action.TryGetProperty("args", out var argsElement) ? argsElement : default;
        if (verb == "move_player" && TryFloat(args, "x", out var x) && TryFloat(args, "z", out var z))
        {
            if (!dryRun)
            {
                _playerX = x;
                _playerZ = z;
            }

            return Success(index, dryRun);
        }

        if (verb == "set_time_scale" && TryFloat(args, "timeScale", out var timeScale) && timeScale is >= 0 and <= 10)
        {
            if (!dryRun)
            {
                _timeScale = timeScale;
            }

            return Success(index, dryRun);
        }

        if (verb == "interact" && args.ValueKind == JsonValueKind.Object &&
            args.TryGetProperty("target", out var targetElement) && targetElement.GetString() is "door" or "treasure")
        {
            var target = targetElement.GetString();
            if (!dryRun)
            {
                _doorOpen |= target == "door";
                _treasureCollected |= target == "treasure";
            }

            return Success(index, dryRun);
        }

        return new
        {
            index,
            ok = false,
            state = "failed",
            error = new { code = "ACTION_NOT_ALLOWED", message = "Action or arguments are not allowed." }
        };
    }

    private static object Success(int index, bool dryRun) => new
    {
        index,
        ok = true,
        state = dryRun ? "validated" : "completed",
        error = (object?)null
    };

    private static object Entity(string name, string path, int instanceId, float x, float z, string tag) => new
    {
        id = StableEntityId.Create("Simulator", path, instanceId),
        kind = "gameObject",
        name,
        path,
        instanceId,
        active = true,
        tags = new[] { tag },
        pose = new { position = new { x, y = 0, z }, rotation = new { x = 0, y = 0, z = 0, w = 1 } },
        actions = Array.Empty<string>()
    };

    private static object[] Descriptors() => new object[]
    {
        new { verb = "interact", description = "Open the door or collect the treasure.", targets = new[] { "entity" }, args = new[] { new { name = "target", type = "string", required = true } }, modes = new[] { "instant" } },
        new { verb = "move_player", description = "Move the simulated player.", targets = new[] { "player" }, args = new[] { new { name = "x", type = "number", required = true }, new { name = "z", type = "number", required = true } }, modes = new[] { "instant" } },
        new { verb = "set_time_scale", description = "Set simulated Unity time scale.", targets = new[] { "system" }, args = new[] { new { name = "timeScale", type = "number", required = true } }, modes = new[] { "instant" } }
    };

    private static bool TryFloat(JsonElement args, string name, out float value)
    {
        value = 0;
        return args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var element) && element.TryGetSingle(out value);
    }

    private static string Error(string requestId, string code, string message) => JsonSerializer.Serialize(new
    {
        schema = BridgeSchemas.BridgeV1,
        requestId,
        ok = false,
        result = (object?)null,
        error = new { code, message, details = new { } }
    }, JsonOptions);

    public static void SelfTest()
    {
        var state = new SimulatorState();
        var result = state.Act("{\"requestId\":\"test\",\"payload\":{\"actions\":[{\"verb\":\"interact\",\"args\":{\"target\":\"treasure\"}}]}}");
        if (!result.Contains("completed", StringComparison.Ordinal) ||
            !state.Snapshot("after").Contains("Treasure collected", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Simulator action did not change the snapshot.");
        }
    }
}
