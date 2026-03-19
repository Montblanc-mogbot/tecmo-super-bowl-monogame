using System;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.Events;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Consumes <see cref="BlockContactEvent"/> and temporarily interrupts both entities into an Engaged state.
///
/// This is scaffolding for later block resolution/animations.
/// </summary>
public sealed class EngagementSystem
{
    // Short, deterministic "hold" duration.
    public float EngagementDurationSeconds = 0.35f;

    // Cooldown prevents re-engaging every tick while still colliding.
    public float EngagementCooldownSeconds = 0.60f;

    // Contact distance gate (mirrors legacy system constant; keep generous for now).
    public float ContactDistancePixels = 6f;

    public void Update(World world, float dtSeconds)
    {
        // Tick cooldown timers.
        if (dtSeconds > 0f)
        {
            var qEng = new QueryDescription().WithAll<Engagement>();
            world.Query(in qEng, (Entity e, ref Engagement eng) =>
            {
                if (eng.CooldownSeconds <= 0f)
                    return;

                eng.CooldownSeconds = MathF.Max(0f, eng.CooldownSeconds - dtSeconds);
                if (eng.CooldownSeconds <= 0f)
                    eng.PartnerEntityId = -1;
            });
        }

        // Resolve contact -> engagement.
        foreach (var evt in SimEventBus.Drain<BlockContactEvent>())
        {
            var blockerId = evt.BlockerId;
            var defenderId = evt.DefenderId;

            if (!TryGet(world, blockerId, out var blockerB, out var blockerStack, out var blockerEng, out var blockerPos))
                continue;
            if (!TryGet(world, defenderId, out var defenderB, out var defenderStack, out var defenderEng, out var defenderPos))
                continue;

            // Gate: if either is on cooldown, ignore.
            if (blockerEng.CooldownSeconds > 0f || defenderEng.CooldownSeconds > 0f)
                continue;

            // Gate: don't stack multiple engagements.
            if (blockerStack.HasActive(BehaviorInterruptKind.Engagement) || defenderStack.HasActive(BehaviorInterruptKind.Engagement))
                continue;

            // Distance gate.
            var distSq = Vector2.DistanceSquared(blockerPos.Value, defenderPos.Value);
            if (distSq > ContactDistancePixels * ContactDistancePixels)
                continue;

            // Begin engagement (interrupt both).
            BeginEngagement(world, blockerId, defenderId);
        }
    }

    private void BeginEngagement(World world, int blockerId, int defenderId)
    {
        InterruptIntoEngaged(world, blockerId, defenderId);
        InterruptIntoEngaged(world, defenderId, blockerId);

        SetEngagement(world, blockerId, partnerId: defenderId, cooldownSeconds: EngagementCooldownSeconds);
        SetEngagement(world, defenderId, partnerId: blockerId, cooldownSeconds: EngagementCooldownSeconds);

        Console.WriteLine($"[sim-arch] interrupt begin kind=Engagement blocker={blockerId} defender={defenderId}");
    }

    private void InterruptIntoEngaged(World world, int entityId, int partnerId)
    {
        var q = new QueryDescription().WithAll<Behavior, BehaviorStack>();
        world.Query(in q, (Entity e, ref Behavior b, ref BehaviorStack stack) =>
        {
            if (e.Id != entityId)
                return;

            BehaviorInterrupt.Push(ref b, ref stack, BehaviorInterruptKind.Engagement, durationSeconds: EngagementDurationSeconds);

            b.State = BehaviorState.Engaged;
            b.StateTimer = EngagementDurationSeconds;
            b.TargetEntityId = partnerId;
        });
    }

    private static void SetEngagement(World world, int entityId, int partnerId, float cooldownSeconds)
    {
        var q = new QueryDescription().WithAll<Engagement>();
        world.Query(in q, (Entity e, ref Engagement eng) =>
        {
            if (e.Id != entityId)
                return;

            eng.PartnerEntityId = partnerId;
            eng.CooldownSeconds = cooldownSeconds;
        });
    }

    private static bool TryGet(World world, int id, out Behavior b, out BehaviorStack stack, out Engagement eng, out Position pos)
    {
        b = default;
        stack = default;
        eng = default;
        pos = default;

        var found = false;
        var bLocal = default(Behavior);
        var sLocal = default(BehaviorStack);
        var eLocal = default(Engagement);
        var pLocal = default(Position);

        var q = new QueryDescription().WithAll<Behavior, BehaviorStack, Engagement, Position>();
        world.Query(in q, (Entity e, ref Behavior bb, ref BehaviorStack ss, ref Engagement ee, ref Position pp) =>
        {
            if (found)
                return;
            if (e.Id != id)
                return;

            bLocal = bb;
            sLocal = ss;
            eLocal = ee;
            pLocal = pp;
            found = true;
        });

        if (!found)
            return false;

        b = bLocal;
        stack = sLocal;
        eng = eLocal;
        pos = pLocal;
        return true;
    }
}
