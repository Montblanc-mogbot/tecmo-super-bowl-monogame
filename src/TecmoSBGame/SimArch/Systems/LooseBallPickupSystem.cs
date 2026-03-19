using System;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.Events;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Resolves loose ball pickup when players overlap the ball.
///
/// Ported conceptually from: ArchiveMge/Systems/LooseBallPickupSystem.cs
/// </summary>
public sealed class LooseBallPickupSystem
{
    public float PickupRadius = 8f;

    public void Update(World world, int ballEntityId, ref Control control)
    {
        if (!TryGetLooseBallPosition(world, ballEntityId, out var ballPos))
            return;

        var bestId = -1;
        var bestDist = float.PositiveInfinity;

        var q = new QueryDescription().WithAll<Position, Team>();
        world.Query(in q, (Entity e, ref Position p, ref Team _) =>
        {
            var d = Vector2.DistanceSquared(p.Value, ballPos);
            var score = d + (e.Id * 0.0001f);
            if (score < bestDist)
            {
                bestDist = score;
                bestId = e.Id;
            }
        });

        if (bestId < 0)
            return;

        if (bestDist > PickupRadius * PickupRadius)
            return;

        // Assign ball.
        var qBall = new QueryDescription().WithAll<Ball, Velocity>();
        world.Query(in qBall, (Entity e, ref Ball b, ref Velocity v) =>
        {
            if (e.Id != ballEntityId)
                return;

            if (b.State != BallState.Loose)
                return;

            b.State = BallState.Held;
            b.OwnerEntityId = bestId;
            b.FlightKind = BallFlightKind.None;
            b.IsComplete = true;
            b.Height = 0f;

            v.Value = Vector2.Zero;
        });

        var ev = new LooseBallPickupEvent(PickerId: bestId, BallPosition: ballPos);
        SimEventBus.Send(ref ev);

        // Force control to picker.
        control.PendingForcedEntityId = bestId;
        control.ControlledEntityId = bestId;

        Console.WriteLine($"[sim-arch] loose-ball pickup picker={bestId}");
    }

    private static bool TryGetLooseBallPosition(World world, int ballEntityId, out Vector2 pos)
    {
        pos = default;
        var found = false;
        var local = Vector2.Zero;

        var q = new QueryDescription().WithAll<Ball, Position>();
        world.Query(in q, (Entity e, ref Ball b, ref Position p) =>
        {
            if (found)
                return;
            if (e.Id != ballEntityId)
                return;
            if (b.State != BallState.Loose)
                return;

            local = p.Value;
            found = true;
        });

        if (!found)
            return false;

        pos = local;
        return true;
    }
}
