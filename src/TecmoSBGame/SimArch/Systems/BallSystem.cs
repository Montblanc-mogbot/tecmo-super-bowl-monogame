using System;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Arch-sim port of <c>TecmoSBGame.Systems.BallPhysicsSystem</c>.
///
/// Deterministic ball-only physics:
/// - Held: glue ball position to owner.
/// - InFlight (FlightKind != None): parametric XY lerp from StartPos->EndPos with a visual-only height parabola.
/// - Loose/InAir (no flight): integrate constant velocity.
///
/// Notes:
/// - This system only updates motion/state fields needed for motion.
/// - Catch/whistle/rules logic should live in higher-level systems.
/// - Velocity is interpreted as "units per 60Hz tick" to match existing MGE system behavior.
/// </summary>
public sealed class BallSystem
{
    private static bool TryGetOwnerPosition(World world, int ownerEntityId, out Vector2 pos)
    {
        pos = default;
        if (ownerEntityId == 0)
            return false;

        var found = false;
        var result = Vector2.Zero;

        var q = new QueryDescription().WithAll<Position>();
        world.Query(in q, (Entity e, ref Position p) =>
        {
            if (found)
                return;
            if (e.Id != ownerEntityId)
                return;

            result = p.Value;
            found = true;
        });

        if (!found)
            return false;

        pos = result;
        return true;
    }

    public void Update(World world, float dtSeconds)
    {
        if (dtSeconds <= 0f)
            return;

        var tickScale = dtSeconds * 60f;

        var query = new QueryDescription().WithAll<Ball, Position, Velocity>();

        world.Query(in query, (Entity _, ref Ball b, ref Position pos, ref Velocity vel) =>
        {
            // Held: glue to owner.
            if (b.State == TecmoSBGame.State.BallState.Held && b.OwnerEntityId != 0)
            {
                if (TryGetOwnerPosition(world, b.OwnerEntityId, out var ownerPos))
                {
                    pos.Value = ownerPos;
                    vel.Value = Vector2.Zero;

                    b.Height = 0f;
                    b.IsComplete = true;
                    b.FlightKind = BallFlightKind.None;
                    b.DurationSeconds = 0f;
                    b.ElapsedSeconds = 0f;
                }

                return;
            }

            // In flight: override XY by parametric model.
            if (b.FlightKind != BallFlightKind.None)
            {
                b.ElapsedSeconds = MathF.Min(b.DurationSeconds, b.ElapsedSeconds + dtSeconds);

                var s = b.DurationSeconds <= 0.0001f
                    ? 1f
                    : MathHelper.Clamp(b.ElapsedSeconds / b.DurationSeconds, 0f, 1f);

                pos.Value = Vector2.Lerp(b.StartPos, b.EndPos, s);

                // Visual-only height parabola.
                b.Height = 4f * b.ApexHeight * s * (1f - s);

                b.IsComplete = s >= 1f;

                // While in flight we do not use the velocity integrator.
                vel.Value = Vector2.Zero;
                return;
            }

            // Loose or in-air without a flight component: constant velocity integration.
            // Velocity is in "units per 60Hz tick".
            if (b.State is TecmoSBGame.State.BallState.InAir or TecmoSBGame.State.BallState.Loose)
            {
                pos.Value += vel.Value * tickScale;
            }
        });
    }
}
