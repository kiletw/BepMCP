using System.Collections.Generic;

namespace UnityMcp.Bridge;

public static class BridgeSchemas
{
    public const string BridgeV1 = "unity-mcp.bridge/1";
    public const string ProfileV1 = "unity-mcp.profile/1";
}

public sealed class BridgeRequest<TPayload>
{
    public string Schema { get; set; } = BridgeSchemas.BridgeV1;
    public string RequestId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public TPayload? Payload { get; set; }
}

public sealed class BridgeResponse<TResult>
{
    public string Schema { get; set; } = BridgeSchemas.BridgeV1;
    public string RequestId { get; set; } = string.Empty;
    public bool Ok { get; set; }
    public TResult? Result { get; set; }
    public BridgeError? Error { get; set; }
}

public sealed class BridgeError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string> Details { get; set; } = new Dictionary<string, string>();
}

public sealed class SnapshotRequest
{
    public bool IncludeEntities { get; set; } = true;
    public bool IncludeScreen { get; set; }
    public bool IncludeUi { get; set; }
    public int MaxEntities { get; set; } = 200;
}

public sealed class SnapshotResult
{
    public RuntimeInfo Runtime { get; set; } = new RuntimeInfo();
    public GameInfo Game { get; set; } = new GameInfo();
    public AgentInfo Agent { get; set; } = new AgentInfo();
    public List<EntityInfo> Entities { get; set; } = new List<EntityInfo>();
    public List<ActionDescriptor> Actions { get; set; } = new List<ActionDescriptor>();
}

public sealed class RuntimeInfo
{
    public string UnityVersion { get; set; } = string.Empty;
    public string ScriptingBackend { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
}

public sealed class GameInfo
{
    public string Id { get; set; } = "unknown-game";
    public string Scene { get; set; } = string.Empty;
    public float TimeScale { get; set; } = 1;
}

public sealed class AgentInfo
{
    public TargetRef Target { get; set; } = new TargetRef { Kind = "player", Id = "local" };
    public Pose Pose { get; set; } = new Pose();
}

public sealed class EntityInfo
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = "entity";
    public string Name { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public Pose Pose { get; set; } = new Pose();
    public List<string> Actions { get; set; } = new List<string>();
}

public sealed class ActionRequest
{
    public List<ActionCommand> Actions { get; set; } = new List<ActionCommand>();
    public int TimeoutMs { get; set; } = 1000;
    public bool DryRun { get; set; }
}

public sealed class ActionCommand
{
    public TargetRef Target { get; set; } = new TargetRef();
    public string Verb { get; set; } = string.Empty;
    public Dictionary<string, string> Args { get; set; } = new Dictionary<string, string>();
    public ActionMode Mode { get; set; } = new ActionMode();
}

public sealed class TargetRef
{
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
}

public sealed class ActionMode
{
    public string Type { get; set; } = "instant";
    public int Ms { get; set; }
}

public sealed class ActionDescriptor
{
    public string Verb { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Targets { get; set; } = new List<string>();
    public List<ActionArgDescriptor> Args { get; set; } = new List<ActionArgDescriptor>();
    public List<string> Modes { get; set; } = new List<string>();
}

public sealed class ActionArgDescriptor
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public bool Required { get; set; }
}

public sealed class ActionExecutionResult
{
    public int Index { get; set; }
    public bool Ok { get; set; }
    public string State { get; set; } = "completed";
    public BridgeError? Error { get; set; }
}

public sealed class Pose
{
    public Vec3 Position { get; set; } = new Vec3();
    public Quat Rotation { get; set; } = new Quat();
}

public sealed class Vec3
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

public sealed class Quat
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float W { get; set; } = 1;
}
