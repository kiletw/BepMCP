using System;
using UnityMcp.Bridge;
using UnityMcp.Core;

namespace UnityMcp.Core.Tests;

public static class Program
{
    public static void Main()
    {
        RegistryExecutesOnlyRegisteredActions();
        SchedulerRunsOnCallerPump();
        ProfilesIgnoreDisabledActions();
        Console.WriteLine("UnityMcp.Core.Tests ok");
    }

    private static void RegistryExecutesOnlyRegisteredActions()
    {
        var registry = new ActionRegistry();
        registry.Register(new ActionDescriptor { Verb = "ping" }, context =>
            context.DryRun ? ActionRegistry.DryRun(context.Index) : ActionRegistry.Completed(context.Index));

        var accepted = registry.Execute(new ActionCommand { Verb = "ping" }, 0, false);
        Assert(accepted.Ok && accepted.State == "completed", "registered action should run");

        var rejected = registry.Execute(new ActionCommand { Verb = "missing" }, 1, false);
        Assert(!rejected.Ok && rejected.Error != null && rejected.Error.Code == "ACTION_NOT_ALLOWED",
            "missing action should be rejected");
    }

    private static void SchedulerRunsOnCallerPump()
    {
        var scheduler = new MainThreadScheduler();
        var task = scheduler.Enqueue(() => 40 + 2);

        Assert(!task.IsCompleted, "work should wait for pump");
        Assert(scheduler.RunPending(1) == 1, "one item should run");
        Assert(task.Result == 42, "scheduled work should return result");
    }

    private static void ProfilesIgnoreDisabledActions()
    {
        var profile = new UnityMcpProfile();
        profile.Actions.Add(new ProfileAction { Verb = "jump", Enabled = true });
        profile.Actions.Add(new ProfileAction { Verb = "candidate_secret", Enabled = false });

        var actions = ProfileDescriptors.ListEnabledActions(profile);
        Assert(actions.Count == 1 && actions[0].Verb == "jump", "disabled actions must stay hidden");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
