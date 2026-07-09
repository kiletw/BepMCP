using System;
using System.Globalization;
using System.Text;
using System.Text.Json;
using UnityEngine;
using UnityMcp.Bridge;

namespace UnityMcp.Plugin.BepInEx6.Il2Cpp;

internal static class UnityActions
{
    public static string Execute(string body, string fallbackRequestId)
    {
        string requestId;
        bool dryRun;
        JsonElement actions;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            requestId = TryGetString(root, "requestId") ?? fallbackRequestId;
            var payload = root.GetProperty("payload");
            dryRun = payload.TryGetProperty("dryRun", out var dryRunElement) && dryRunElement.GetBoolean();
            actions = payload.GetProperty("actions").Clone();
        }
        catch (Exception ex)
        {
            return BridgeJson.Error(fallbackRequestId, "INVALID_JSON", ex.Message);
        }

        if (actions.ValueKind != JsonValueKind.Array)
        {
            return BridgeJson.Error(requestId, "INVALID_ACTION", "Payload actions are required.");
        }

        var builder = new StringBuilder(512);
        builder.Append("{\"schema\":\"").Append(BridgeSchemas.BridgeV1).Append("\",");
        builder.Append("\"requestId\":\"").Append(BridgeJson.Escape(requestId)).Append("\",");
        builder.Append("\"ok\":true,\"result\":{\"actions\":[");

        var i = 0;
        foreach (var action in actions.EnumerateArray())
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            AppendActionResult(builder, action, i, dryRun);
            i++;
        }

        builder.Append("]},\"error\":null}");
        return builder.ToString();
    }

    private static void AppendActionResult(StringBuilder builder, JsonElement action, int index, bool dryRun)
    {
        var verb = TryGetString(action, "verb");
        if (string.IsNullOrEmpty(verb))
        {
            AppendFailed(builder, index, "INVALID_ACTION", "Action verb is required.");
            return;
        }

        if (verb == "set_time_scale")
        {
            var timeScale = 1f;
            if (action.TryGetProperty("args", out var args) &&
                args.TryGetProperty("timeScale", out var timeScaleElement) &&
                timeScaleElement.TryGetSingle(out var parsed))
            {
                timeScale = parsed;
            }

            if (!dryRun)
            {
                Time.timeScale = timeScale;
            }

            builder.Append("{\"index\":").Append(index);
            builder.Append(",\"ok\":true,\"state\":\"").Append(dryRun ? "validated" : "completed").Append("\",");
            builder.Append("\"events\":[{\"type\":\"time_scale\",\"value\":");
            builder.Append(timeScale.ToString(CultureInfo.InvariantCulture));
            builder.Append("}],\"error\":null}");
            return;
        }

        AppendFailed(builder, index, "ACTION_NOT_ALLOWED", "Action is not registered.");
    }

    private static void AppendFailed(StringBuilder builder, int index, string code, string message)
    {
        builder.Append("{\"index\":").Append(index);
        builder.Append(",\"ok\":false,\"state\":\"failed\",\"events\":[],\"error\":{\"code\":\"");
        builder.Append(BridgeJson.Escape(code)).Append("\",\"message\":\"");
        builder.Append(BridgeJson.Escape(message)).Append("\",\"details\":{}}}");
    }

    private static string? TryGetString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }
}
