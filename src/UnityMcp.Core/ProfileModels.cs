using System.Collections.Generic;
using UnityMcp.Bridge;

namespace UnityMcp.Core;

public sealed class UnityMcpProfile
{
    public string Schema { get; set; } = BridgeSchemas.ProfileV1;
    public string GameId { get; set; } = "unknown-game";
    public ProfileMatch Match { get; set; } = new ProfileMatch();
    public List<NamedSelector> Selectors { get; set; } = new List<NamedSelector>();
    public ProfileSnapshot Snapshot { get; set; } = new ProfileSnapshot();
    public List<ProfileAction> Actions { get; set; } = new List<ProfileAction>();
    public List<ProfileAlias> Aliases { get; set; } = new List<ProfileAlias>();
    public ProfileSafety Safety { get; set; } = new ProfileSafety();
}

public sealed class ProfileMatch
{
    public string ProcessName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
}

public sealed class NamedSelector
{
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public sealed class ProfileSnapshot
{
    public int MaxEntities { get; set; } = 200;
    public List<string> IncludeTags { get; set; } = new List<string>();
    public List<string> IncludeLayers { get; set; } = new List<string>();
}

public sealed class ProfileAction
{
    public string Verb { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Backend { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DeclaringType { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public int TimeoutMs { get; set; } = 1000;
    public string Reason { get; set; } = string.Empty;
}

public sealed class ProfileAlias
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
}

public sealed class ProfileSafety
{
    public int MaxActionMs { get; set; } = 3000;
    public bool AllowMethodCalls { get; set; }
}

public static class ProfileDescriptors
{
    public static IReadOnlyList<ActionDescriptor> ListEnabledActions(UnityMcpProfile profile)
    {
        var result = new List<ActionDescriptor>();
        if (profile == null)
        {
            return result;
        }

        foreach (var action in profile.Actions)
        {
            if (!action.Enabled || string.IsNullOrWhiteSpace(action.Verb))
            {
                continue;
            }

            result.Add(new ActionDescriptor
            {
                Verb = action.Verb,
                Description = action.Description,
                Targets = new List<string> { "player", "entity", "ui", "system" },
                Modes = new List<string> { "instant", "duration" }
            });
        }

        return result;
    }
}
