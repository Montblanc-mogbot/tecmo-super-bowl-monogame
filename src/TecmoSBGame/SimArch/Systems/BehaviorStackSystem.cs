using System;
using Arch.Core;
using Arch.Core.Extensions;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

// Ported from: src/TecmoSBGame/ArchiveMge/Systems/BehaviorStackSystem.cs

/// <summary>
/// Decrements the active interrupt timer (top of stack) and restores the prior behavior when it expires.
/// </summary>
public sealed class BehaviorStackSystem
{
    public void Update(World world, float dtSeconds)
    {
        if (dtSeconds <= 0f)
            return;

        var q = new QueryDescription().WithAll<Behavior, BehaviorStack>();

        world.Query(in q, (Entity e, ref Behavior b, ref BehaviorStack stack) =>
        {
            if (!stack.TryPeek(out var top))
                return;

            var remaining = top.RemainingSeconds - dtSeconds;
            if (remaining > 0f)
            {
                // Update remaining time on top entry.
                if (stack.Count == 1)
                {
                    stack.E0 = new BehaviorStackEntry { Kind = top.Kind, Saved = top.Saved, RemainingSeconds = remaining };
                }
                else
                {
                    stack.E1 = new BehaviorStackEntry { Kind = top.Kind, Saved = top.Saved, RemainingSeconds = remaining };
                }

                return;
            }

            // Expired: pop and restore.
            if (!stack.TryPop(out var popped))
                return;

            BehaviorInterrupt.Restore(ref b, popped.Saved);
            Console.WriteLine($"[sim-arch] interrupt end kind={popped.Kind} entity={e.Id}");
        });
    }
}
