using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMcp.Bridge;
#if BEPMCP_UGUI
using UnityEngine.UI;
#endif

namespace UnityMcp.Plugin.BepInEx6.Il2Cpp;

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
        AppendProperty(builder, "scriptingBackend", "il2cpp", true);
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
            AppendObject(builder, root, scene.name, string.Empty, maxEntities, ref count);
            if (count >= maxEntities)
            {
                break;
            }
        }

        builder.Append("],\"actions\":[{\"verb\":\"set_time_scale\",\"description\":\"Set Unity Time.timeScale.\",");
        builder.Append("\"targets\":[\"system\"],\"args\":[{\"name\":\"timeScale\",\"type\":\"number\",\"required\":true}],");
        builder.Append("\"modes\":[\"instant\"]}]}");
        builder.Append(",\"error\":null}");
        return builder.ToString();
    }

    private static void AppendObject(StringBuilder builder, GameObject gameObject, string sceneName, string parentPath, int max, ref int count)
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
        var instanceId = gameObject.GetInstanceID();
        var rectTransform = gameObject.GetComponent<RectTransform>();

        builder.Append('{');
        AppendProperty(builder, "id", StableEntityId.Create(sceneName, path, instanceId), true);
        AppendProperty(builder, "kind", rectTransform == null ? "gameObject" : "ui", true);
        AppendProperty(builder, "name", gameObject.name, true);
        AppendProperty(builder, "path", path, true);
        builder.Append("\"instanceId\":").Append(instanceId).Append(',');
        builder.Append("\"layer\":").Append(gameObject.layer).Append(',');
        builder.Append("\"active\":").Append(gameObject.activeInHierarchy ? "true" : "false").Append(',');
        builder.Append("\"pose\":{");
        AppendVector(builder, "position", transform.position, true);
        AppendQuaternion(builder, "rotation", transform.rotation, false);
        builder.Append("},");
        if (rectTransform != null)
        {
            AppendUi(builder, gameObject, rectTransform);
            builder.Append(',');
        }

        builder.Append("\"actions\":[]}");
        count++;

        for (var i = 0; i < transform.childCount && count < max; i++)
        {
            AppendObject(builder, transform.GetChild(i).gameObject, sceneName, path, max, ref count);
        }
    }

    private static void AppendUi(StringBuilder builder, GameObject gameObject, RectTransform transform)
    {
        var camera = Camera.main;
        var canvas = gameObject.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            camera = null;
        }

        var rect = transform.rect;
        var points = new[]
        {
            RectTransformUtility.WorldToScreenPoint(camera, transform.TransformPoint(new Vector3(rect.xMin, rect.yMin))),
            RectTransformUtility.WorldToScreenPoint(camera, transform.TransformPoint(new Vector3(rect.xMin, rect.yMax))),
            RectTransformUtility.WorldToScreenPoint(camera, transform.TransformPoint(new Vector3(rect.xMax, rect.yMin))),
            RectTransformUtility.WorldToScreenPoint(camera, transform.TransformPoint(new Vector3(rect.xMax, rect.yMax)))
        };
        var minX = Mathf.Min(points[0].x, points[1].x, points[2].x, points[3].x);
        var maxX = Mathf.Max(points[0].x, points[1].x, points[2].x, points[3].x);
        var minY = Mathf.Min(points[0].y, points[1].y, points[2].y, points[3].y);
        var maxY = Mathf.Max(points[0].y, points[1].y, points[2].y, points[3].y);
        var components = gameObject.GetComponents<Component>();
        var names = new List<string>(components.Length);
        var text = string.Empty;
        var interactable = false;

        foreach (var component in components)
        {
            if (component == null)
            {
                continue;
            }

            var type = component.GetType();
            names.Add(type.Name);
            try
            {
                var textProperty = type.GetProperty("text");
                if (string.IsNullOrEmpty(text) && textProperty?.PropertyType == typeof(string))
                {
                    text = textProperty.GetValue(component, null) as string ?? string.Empty;
                }

                var interactableProperty = type.GetProperty("interactable");
                if (interactableProperty?.PropertyType == typeof(bool) &&
                    interactableProperty.GetValue(component, null) is bool enabled)
                {
                    interactable |= enabled;
                }
            }
            catch
            {
                // Some IL2CPP component wrappers do not expose managed reflection metadata.
            }
        }

#if BEPMCP_UGUI
        var selectable = gameObject.GetComponent<Selectable>();
        if (selectable != null)
        {
            interactable = selectable.interactable;
            names.Add(selectable.GetType().Name);
        }

        var legacyText = gameObject.GetComponent<Text>();
        if (legacyText != null)
        {
            text = legacyText.text ?? string.Empty;
            names.Add("Text");
        }
#endif
#if BEPMCP_TMP
        if (string.IsNullOrEmpty(text))
        {
            var tmpText = gameObject.GetComponent<TMPro.TMP_Text>();
            if (tmpText != null)
            {
                text = tmpText.text ?? string.Empty;
                names.Add("TMP_Text");
            }
        }
#endif

        builder.Append("\"ui\":{");
        AppendProperty(builder, "text", text, true);
        builder.Append("\"interactable\":").Append(interactable ? "true" : "false").Append(',');
        var visible = gameObject.activeInHierarchy && maxX > 0 && maxY > 0 && minX < Screen.width && minY < Screen.height;
        builder.Append("\"visible\":").Append(visible ? "true" : "false").Append(',');
        builder.Append("\"bounds\":{");
        AppendNumber(builder, "x", minX, true);
        AppendNumber(builder, "y", Screen.height - maxY, true);
        AppendNumber(builder, "width", maxX - minX, true);
        AppendNumber(builder, "height", maxY - minY, false);
        builder.Append("},\"components\":[");
        for (var i = 0; i < names.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            BridgeJson.AppendString(builder, names[i]);
        }

        builder.Append("]}");
    }

    private static void AppendNumber(StringBuilder builder, string name, float value, bool trailingComma)
    {
        BridgeJson.AppendString(builder, name);
        builder.Append(':').Append(value.ToString(CultureInfo.InvariantCulture));
        if (trailingComma)
        {
            builder.Append(',');
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
