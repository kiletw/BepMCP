using System;
using System.Collections.Generic;
using UnityMcp.Bridge;

namespace UnityMcp.Core;

public delegate ActionExecutionResult ActionHandler(ActionExecutionContext context);

public sealed class ActionExecutionContext
{
    public ActionExecutionContext(ActionCommand command, int index, bool dryRun)
    {
        Command = command;
        Index = index;
        DryRun = dryRun;
    }

    public ActionCommand Command { get; }
    public int Index { get; }
    public bool DryRun { get; }
}

public sealed class ActionRegistration
{
    public ActionRegistration(ActionDescriptor descriptor, ActionHandler handler)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public ActionDescriptor Descriptor { get; }
    public ActionHandler Handler { get; }
}

public sealed class ActionRegistry
{
    private readonly Dictionary<string, ActionRegistration> _actions =
        new Dictionary<string, ActionRegistration>(StringComparer.OrdinalIgnoreCase);

    public void Register(ActionDescriptor descriptor, ActionHandler handler)
    {
        if (descriptor == null)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }

        if (string.IsNullOrWhiteSpace(descriptor.Verb))
        {
            throw new ArgumentException("Action verb is required.", nameof(descriptor));
        }

        _actions[descriptor.Verb] = new ActionRegistration(descriptor, handler);
    }

    public IReadOnlyList<ActionDescriptor> List()
    {
        var result = new List<ActionDescriptor>(_actions.Count);
        foreach (var item in _actions.Values)
        {
            result.Add(item.Descriptor);
        }

        result.Sort((left, right) => string.CompareOrdinal(left.Verb, right.Verb));
        return result;
    }

    public ActionExecutionResult Execute(ActionCommand command, int index, bool dryRun)
    {
        if (command == null || string.IsNullOrWhiteSpace(command.Verb))
        {
            return Failure(index, "INVALID_ACTION", "Action verb is required.");
        }

        if (!_actions.TryGetValue(command.Verb, out var registration))
        {
            return Failure(index, "ACTION_NOT_ALLOWED", "Action is not registered.");
        }

        return registration.Handler(new ActionExecutionContext(command, index, dryRun));
    }

    public static ActionExecutionResult Completed(int index)
    {
        return new ActionExecutionResult
        {
            Index = index,
            Ok = true,
            State = "completed"
        };
    }

    public static ActionExecutionResult DryRun(int index)
    {
        return new ActionExecutionResult
        {
            Index = index,
            Ok = true,
            State = "validated"
        };
    }

    public static ActionExecutionResult Failure(int index, string code, string message)
    {
        return new ActionExecutionResult
        {
            Index = index,
            Ok = false,
            State = "failed",
            Error = new BridgeError { Code = code, Message = message }
        };
    }
}
