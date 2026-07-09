using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityMcp.Bridge;

namespace UnityMcp.Plugin.BepInEx5.Mono;

internal static class UnityActions
{
    public static string Execute(string body, string fallbackRequestId)
    {
        ActEnvelope? envelope;
        try
        {
            envelope = JsonUtility.FromJson<ActEnvelope>(body);
        }
        catch (Exception ex)
        {
            return BridgeJson.Error(fallbackRequestId, "INVALID_JSON", ex.Message);
        }

        var requestId = string.IsNullOrEmpty(envelope?.requestId) ? fallbackRequestId : envelope.requestId;
        var payload = envelope?.payload;
        if (payload?.actions == null)
        {
            return BridgeJson.Error(requestId, "INVALID_ACTION", "Payload actions are required.");
        }

        var builder = new StringBuilder(512);
        builder.Append("{\"schema\":\"").Append(BridgeSchemas.BridgeV1).Append("\",");
        builder.Append("\"requestId\":\"").Append(BridgeJson.Escape(requestId)).Append("\",");
        builder.Append("\"ok\":true,\"result\":{\"actions\":[");

        for (var i = 0; i < payload.actions.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            AppendActionResult(builder, payload.actions[i], i, payload.dryRun);
        }

        builder.Append("]},\"error\":null}");
        return builder.ToString();
    }

    private static void AppendActionResult(StringBuilder builder, ActCommand action, int index, bool dryRun)
    {
        if (action == null || string.IsNullOrEmpty(action.verb))
        {
            AppendFailed(builder, index, "INVALID_ACTION", "Action verb is required.");
            return;
        }

        if (action.verb == "set_time_scale")
        {
            if (!dryRun)
            {
                Time.timeScale = action.args.timeScale;
            }

            builder.Append("{\"index\":").Append(index);
            builder.Append(",\"ok\":true,\"state\":\"").Append(dryRun ? "validated" : "completed").Append("\",");
            builder.Append("\"events\":[{\"type\":\"time_scale\",\"value\":");
            builder.Append(action.args.timeScale.ToString(CultureInfo.InvariantCulture));
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

    [Serializable]
    private sealed class ActEnvelope
    {
        public string requestId = string.Empty;
        public ActPayload payload = new ActPayload();
    }

    [Serializable]
    private sealed class ActPayload
    {
        public bool dryRun;
        public ActCommand[] actions = Array.Empty<ActCommand>();
    }

    [Serializable]
    private sealed class ActCommand
    {
        public string verb = string.Empty;
        public ActArgs args = new ActArgs();
    }

    [Serializable]
    private sealed class ActArgs
    {
        public float timeScale = 1;
    }
}
