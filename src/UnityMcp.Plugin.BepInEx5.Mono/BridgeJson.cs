using System.Text;
using UnityMcp.Bridge;

namespace UnityMcp.Plugin.BepInEx5.Mono;

internal static class BridgeJson
{
    public static string Error(string requestId, string code, string message)
    {
        return "{\"schema\":\"" + BridgeSchemas.BridgeV1 +
               "\",\"requestId\":\"" + Escape(requestId) +
               "\",\"ok\":false,\"result\":null,\"error\":{\"code\":\"" + Escape(code) +
               "\",\"message\":\"" + Escape(message) +
               "\",\"details\":{}}}";
    }

    public static void AppendString(StringBuilder builder, string value)
    {
        builder.Append('"');
        builder.Append(Escape(value));
        builder.Append('"');
    }

    public static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length + 8);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    builder.Append(ch);
                    break;
            }
        }

        return builder.ToString();
    }
}
