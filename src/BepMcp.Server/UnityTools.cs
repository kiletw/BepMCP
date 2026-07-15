using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace BepMcp.Server;

[McpServerToolType]
public static class UnityTools
{
    [McpServerTool(Name = "unity_health")]
    [Description("Check whether the local Unity bridge is available and return its process ID.")]
    public static Task<string> Health(
        UnityBridgeClient bridge,
        CancellationToken cancellationToken)
    {
        return bridge.HealthAsync(cancellationToken);
    }

    [McpServerTool(Name = "unity_snapshot")]
    [Description("Read the active Unity scene, runtime metadata, entities, and available semantic actions.")]
    public static Task<string> Snapshot(
        UnityBridgeClient bridge,
        CancellationToken cancellationToken)
    {
        return bridge.SnapshotAsync(cancellationToken);
    }

    [McpServerTool(Name = "unity_wait")]
    [Description("Wait until case-insensitive text appears in or disappears from the Unity snapshot, then return the matching snapshot.")]
    public static Task<string> Wait(
        UnityBridgeClient bridge,
        [Description("Scene name, entity path, UI text, or another snapshot value to match.")] string contains,
        [Description("Wait for the text to disappear instead of appear.")] bool absent = false,
        [Description("Maximum wait in milliseconds, from 100 to 30000.")] int timeoutMs = 5000,
        [Description("Snapshot polling interval in milliseconds, from 50 to 1000.")] int pollMs = 200,
        CancellationToken cancellationToken = default)
    {
        return bridge.WaitForSnapshotAsync(contains, absent, timeoutMs, pollMs, cancellationToken);
    }

    [McpServerTool(Name = "unity_screenshot")]
    [Description("Capture the current Unity game frame as a PNG image. Observe this before coordinate input.")]
    public static async Task<ImageContentBlock> Screenshot(
        UnityBridgeClient bridge,
        CancellationToken cancellationToken)
    {
        return ImageContentBlock.FromBytes(
            await bridge.ScreenshotAsync(cancellationToken).ConfigureAwait(false),
            "image/png");
    }

    [McpServerTool(Name = "unity_act")]
    [Description("Execute allowlisted Unity-level semantic actions exposed by the game bridge.")]
    public static Task<string> Act(
        UnityBridgeClient bridge,
        [Description("One to 100 allowlisted bridge actions.")] List<UnityBridgeAction> actions,
        [Description("Validate without changing game state.")] bool dryRun = false,
        [Description("Optional caller-defined request ID.")] string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        return bridge.ActAsync(actions, dryRun, requestId, cancellationToken);
    }

    [McpServerTool(Name = "unity_input")]
    [Description("Send a bounded keyboard and mouse sequence to the Unity game window.")]
    public static async Task<string> Input(
        UnityBridgeClient bridge,
        WindowsInput input,
        [Description("Input steps: key_down, key_up, key_tap, mouse_move, mouse_click, scroll, or wait.")]
        List<InputStep> actions,
        [Description("Optional ID that unity_cancel can cancel.")] string? requestId = null,
        [Description("Maximum sequence duration in milliseconds, from 100 to 30000.")] int timeoutMs = 5000,
        CancellationToken cancellationToken = default)
    {
        var processId = await bridge.ProcessIdAsync(cancellationToken).ConfigureAwait(false);
        return await input.ExecuteAsync(actions, processId, requestId, timeoutMs, cancellationToken)
            .ConfigureAwait(false);
    }

    [McpServerTool(Name = "unity_cancel")]
    [Description("Cancel an active unity_input sequence by its caller-defined request ID.")]
    public static string Cancel(
        WindowsInput input,
        [Description("The requestId passed to unity_input.")] string requestId)
    {
        return JsonSerializer.Serialize(new { requestId, cancelled = input.Cancel(requestId) });
    }
}

public sealed class UnityBridgeAction
{
    [Description("Allowlisted action name returned by unity_snapshot.")]
    public required string Verb { get; init; }

    [Description("Action-specific arguments.")]
    public Dictionary<string, JsonElement>? Args { get; init; }
}

public sealed class InputStep
{
    [Description("key_down, key_up, key_tap, mouse_move, mouse_click, scroll, or wait.")]
    public required string Type { get; init; }

    [Description("Keyboard key for key actions.")]
    public string? Key { get; init; }

    [Description("left, right, or middle for mouse_click.")]
    public string? Button { get; init; }

    [Description("Client-area X coordinate for mouse actions.")]
    public int? X { get; init; }

    [Description("Client-area Y coordinate for mouse actions.")]
    public int? Y { get; init; }

    [Description("Duration for wait or key_tap.")]
    public int? Ms { get; init; }

    [Description("Mouse wheel delta for scroll.")]
    public int? Delta { get; init; }
}
