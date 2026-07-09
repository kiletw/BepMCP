using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMcp.Bridge;

namespace UnityMcp.Plugin.BepInEx5.Mono;

internal static class UnitySnapshot
{
    public static string Capture(string requestId, int maxEntities)
    {
        var scene = SceneManager.GetActiveScene();
        var builder = new StringBuilder(4096);

        builder.Append("{\"schema\":\"").Append(BridgeSchemas.BridgeV1).Append("\",");
        builder.Append("\"requestId\":\"").Append(BridgeJson.Escape(requestId)).Append("\",");
        builder.Append("\"ok\":true,\"result\":{");

        builder.Append("\"runtime\":{");
        AppendProperty(builder, "unityVersion", Application.unityVersion, true);
        AppendProperty(builder, "scriptingBackend", "mono", true);
        AppendProperty(builder, "platform", Application.platform.ToString(), false);
        builder.Append("},");

        builder.Append("\"game\":{");
        AppendProperty(builder, "id", Application.productName, true);
        AppendProperty(builder, "scene", scene.name, true);
        builder.Append("\"timeScale\":").Append(Time.timeScale.ToString(CultureInfo.InvariantCulture));
        builder.Append("},");

        builder.Append("\"entities\":[");
        var count = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            AppendObject(builder, root, string.Empty, maxEntities, ref count);
            if (count >= maxEntities)
            {
                break;
            }
        }

        builder.Append("],\"actions\":[]");
        builder.Append("},\"error\":null}");
        return builder.ToString();
    }

    private static void AppendObject(StringBuilder builder, GameObject gameObject, string parentPath, int max, ref int count)
    {
        if (count >= max)
        {
            return;
        }

        if (count > 0)
        {
            builder.Append(',');
        }

        var path = string.IsNullOrEmpty(parentPath) ? gameObject.name : parentPath + "/" + gameObject.name;
        var transform = gameObject.transform;

        builder.Append('{');
        AppendProperty(builder, "id", "go:" + count, true);
        AppendProperty(builder, "kind", "gameObject", true);
        AppendProperty(builder, "name", gameObject.name, true);
        AppendProperty(builder, "path", path, true);
        builder.Append("\"layer\":").Append(gameObject.layer).Append(',');
        builder.Append("\"active\":").Append(gameObject.activeInHierarchy ? "true" : "false").Append(',');
        builder.Append("\"pose\":{");
        AppendVector(builder, "position", transform.position, true);
        AppendQuaternion(builder, "rotation", transform.rotation, false);
        builder.Append("},\"actions\":[]}");
        count++;

        for (var i = 0; i < transform.childCount && count < max; i++)
        {
            AppendObject(builder, transform.GetChild(i).gameObject, path, max, ref count);
        }
    }

    private static void AppendProperty(StringBuilder builder, string name, string value, bool trailingComma)
    {
        BridgeJson.AppendString(builder, name);
        builder.Append(':');
        BridgeJson.AppendString(builder, value);
        if (trailingComma)
        {
            builder.Append(',');
        }
    }

    private static void AppendVector(StringBuilder builder, string name, Vector3 value, bool trailingComma)
    {
        BridgeJson.AppendString(builder, name);
        builder.Append(":{\"x\":").Append(value.x.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"y\":").Append(value.y.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"z\":").Append(value.z.ToString(CultureInfo.InvariantCulture)).Append('}');
        if (trailingComma)
        {
            builder.Append(',');
        }
    }

    private static void AppendQuaternion(StringBuilder builder, string name, Quaternion value, bool trailingComma)
    {
        BridgeJson.AppendString(builder, name);
        builder.Append(":{\"x\":").Append(value.x.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"y\":").Append(value.y.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"z\":").Append(value.z.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"w\":").Append(value.w.ToString(CultureInfo.InvariantCulture)).Append('}');
        if (trailingComma)
        {
            builder.Append(',');
        }
    }
}
