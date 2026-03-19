using System;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.Events;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Debug-only fumble trigger.
///
/// When ui.Back edge is pressed and the ball is held, forces a fumble:
/// - ball becomes Loose
/// - owner cleared
/// - ball gets a small deterministic velocity impulse
/// - emits <see cref="FumbleEvent"/>
/// </summary>
public sealed class FumbleDebugSystem
{
    public float ImpulseSpeedPerTick = 1.6f;

    public void Update(World world, int ballEntityId, in UiButtons ui)
    {
        if (!ui.Back)
            return;

        var q = new QueryDescription().WithAll<Ball, Position, Velocity>();
        world.Query(in q, (Entity e, ref Ball b, ref Position p, ref Velocity v) =>
        {
            if (e.Id != ballEntityId)
                return;

            if (b.State != BallState.Held || b.OwnerEntityId <= 0)
                return;

            var carrierId = b.OwnerEntityId;

            // Drop ball at carrier position and nudge forward/right deterministically.
            b.State = BallState.Loose;
            b.OwnerEntityId = -1;
            b.FlightKind = BallFlightKind.None;
            b.IsComplete = true;
            b.Height = 0f;

            // Impulse direction: downfield (positive X) with slight Y offset.
            var dir = new Vector2(1f, 0.25f);
            dir.Normalize();
            v.Value = dir * ImpulseSpeedPerTick;
            p.Value = p.Value + new Vector2(0, 0); // explicit (keeps deterministic)

            var fe = new FumbleEvent(CarrierId: carrierId, Cause: "debug");
            SimEventBus.Send(ref fe);

            Console.WriteLine($"[sim-arch] DEBUG fumble carrier={carrierId}");
        });
    }
}
