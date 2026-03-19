using System;
using Arch.Core;
using Arch.Core.Extensions;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.Events;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Consumes <see cref="TackleContactEvent"/> and temporarily interrupts the carrier + defender.
///
/// This is scaffolding for later tackle resolution.
/// </summary>
public sealed class TackleInterruptSystem
{
    public float TackleInterruptDurationSeconds = 0.50f;

    public void Update(World world)
    {
        foreach (var evt in SimEventBus.Drain<TackleContactEvent>())
        {
            var tacklerId = evt.DefenderId;
            var carrierId = evt.BallCarrierId;

            if (!TryGet(world, tacklerId, out var tacklerB, out var tacklerStack))
                continue;
            if (!TryGet(world, carrierId, out var carrierB, out var carrierStack))
                continue;

            // Gate: don't re-interrupt if already in an active tackle interrupt.
            if (tacklerStack.HasActive(BehaviorInterruptKind.Tackle) || carrierStack.HasActive(BehaviorInterruptKind.Tackle))
                continue;

            BeginTackleInterrupt(world, tacklerId, carrierId);
        }
    }

    private void BeginTackleInterrupt(World world, int tacklerId, int carrierId)
    {
        InterruptInto(world, tacklerId, carrierId, BehaviorState.Tackling);
        InterruptInto(world, carrierId, tacklerId, BehaviorState.Grappling);

        Console.WriteLine($"[sim-arch] interrupt begin kind=Tackle tackler={tacklerId} carrier={carrierId}");
    }

    private void InterruptInto(World world, int entityId, int targetId, BehaviorState newState)
    {
        var q = new QueryDescription().WithAll<Behavior, BehaviorStack>();
        world.Query(in q, (Entity e, ref Behavior b, ref BehaviorStack stack) =>
        {
            if (e.Id != entityId)
                return;

            BehaviorInterrupt.Push(ref b, ref stack, BehaviorInterruptKind.Tackle, durationSeconds: TackleInterruptDurationSeconds);

            b.State = newState;
            b.StateTimer = TackleInterruptDurationSeconds;
            b.TargetEntityId = targetId;
        });
    }

    private static bool TryGet(World world, int id, out Behavior b, out BehaviorStack stack)
    {
        b = default;
        stack = default;

        var found = false;
        var bLocal = default(Behavior);
        var sLocal = default(BehaviorStack);

        var q = new QueryDescription().WithAll<Behavior, BehaviorStack>();
        world.Query(in q, (Entity e, ref Behavior bb, ref BehaviorStack ss) =>
        {
            if (found)
                return;
            if (e.Id != id)
                return;

            bLocal = bb;
            sLocal = ss;
            found = true;
        });

        if (!found)
            return false;

        b = bLocal;
        stack = sLocal;
        return true;
    }
}
