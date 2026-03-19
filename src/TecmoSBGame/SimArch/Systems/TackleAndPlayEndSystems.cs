using System;
using Arch.Core;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// SimArch port scaffold for the "tackle -> whistle -> play end -> reset" pipeline.
///
/// Source systems (MonoGame.Extended ECS):
/// - *Tackle* systems (contact + resolution)
/// - PlayEndSystem
/// - NextPlayResetSystem
///
/// SimArch currently has no full rules/game-flow state machine, so this system provides a
/// deterministic minimal core:
/// - Detect a tackle contact when a defender gets within a small radius of the ball carrier.
/// - Convert that into a whistle (ends the play) by setting the ball to Dead.
///
/// Future work:
/// - Emit SimArch events (once EventBus receivers exist)
/// - Model down/distance, turnovers, fumbles, touchdowns
/// - Implement reset-to-presnap semantics without recreating the world
/// </summary>
public sealed class TackleAndPlayEndSystems
{
    // Rough contact radius. (Parity tuning TBD)
    private const float TACKLE_RADIUS = 10f;

    public void Update(World world, int ballEntityId, ref Control control)
    {
        // Find ball state + carrier.
        var ballFound = false;
        var carrierId = 0;
        var carrierPos = Vector2.Zero;

        var qBall = new QueryDescription().WithAll<Ball, Position>();
        world.Query(in qBall, (Entity e, ref Ball b, ref Position p) =>
        {
            if (ballFound)
                return;
            if (e.Id != ballEntityId)
                return;

            ballFound = true;

            if (b.State == TecmoSBGame.State.BallState.Held && b.OwnerEntityId != 0)
            {
                carrierId = b.OwnerEntityId;
                // Note: ball position may lag behind owner if other systems did something odd; we prefer the owner.
            }
        });

        if (!ballFound || carrierId == 0)
            return;

        if (!TryGetPosition(world, carrierId, out carrierPos))
            return;

        var radiusSq = TACKLE_RADIUS * TACKLE_RADIUS;

        // Find nearest defender in radius.
        var bestDefenderId = 0;
        var bestDistSq = float.PositiveInfinity;

        var qDef = new QueryDescription().WithAll<Position, Team>();
        world.Query(in qDef, (Entity e, ref Position p, ref Team t) =>
        {
            if (t.IsOffense)
                return;

            var d = p.Value - carrierPos;
            var distSq = d.LengthSquared();
            if (distSq > radiusSq)
                return;

            if (distSq < bestDistSq - 0.0001f || (MathF.Abs(distSq - bestDistSq) <= 0.0001f && (bestDefenderId == 0 || e.Id < bestDefenderId)))
            {
                bestDefenderId = e.Id;
                bestDistSq = distSq;
            }
        });

        if (bestDefenderId == 0)
            return;

        // Resolve tackle -> whistle -> play end.
        // For now: end play immediately by killing possession and stopping movement.
        WhistleDeadBall(world, ballEntityId, carrierId, ref control);
    }

    private static void WhistleDeadBall(World world, int ballEntityId, int carrierId, ref Control control)
    {
        // Clear velocities (carrier + ball) deterministically.
        var qVel = new QueryDescription().WithAll<Velocity>();
        world.Query(in qVel, (Entity e, ref Velocity v) =>
        {
            if (e.Id == ballEntityId || e.Id == carrierId)
                v.Value = Vector2.Zero;
        });

        // Mark ball dead.
        var qBall = new QueryDescription().WithAll<Ball>();
        world.Query(in qBall, (Entity e, ref Ball b) =>
        {
            if (e.Id != ballEntityId)
                return;

            b.State = TecmoSBGame.State.BallState.Dead;
            b.OwnerEntityId = 0;
            b.FlightKind = BallFlightKind.None;
            b.Height = 0f;
            b.IsComplete = true;
        });

        // Control reset: if user was controlling the carrier, release it.
        if (control.ControlledEntityId == carrierId)
            control.ControlledEntityId = 0;
    }

    private static bool TryGetPosition(World world, int entityId, out Vector2 pos)
    {
        pos = default;
        var found = false;
        var result = Vector2.Zero;

        var q = new QueryDescription().WithAll<Position>();
        world.Query(in q, (Entity e, ref Position p) =>
        {
            if (found)
                return;
            if (e.Id != entityId)
                return;
            result = p.Value;
            found = true;
        });

        if (!found)
            return false;

        pos = result;
        return true;
    }
}
