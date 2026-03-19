using System;
using Arch.Core;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// SimArch equivalent of <c>TecmoSBGame.Systems.PassFlightCompleteSystem</c>.
///
/// Minimal deterministic resolution:
/// - If a receiver is within ELIGIBLE_RADIUS of the ball end position, award possession to the nearest.
/// - Else: mark ball dead.
///
/// This is intentionally lightweight; full catch/intercept/incompletion rules are future work.
/// </summary>
public sealed class PassFlightCompleteSystem
{
    private const float ELIGIBLE_RADIUS = 14f;

    public void Update(World world)
    {
        var qBall = new QueryDescription().WithAll<Ball, Position>();
        world.Query(in qBall, (Entity ballEntity, ref Ball ball, ref Position ballPos) =>
        {
            if (ball.FlightKind != BallFlightKind.Pass || !ball.IsComplete)
                return;

            var pos = ballPos.Value;

            var radiusSq = ELIGIBLE_RADIUS * ELIGIBLE_RADIUS;
            var bestId = 0;
            var bestDistSq = float.PositiveInfinity;

            var qCandidates = new QueryDescription().WithAll<Position, Team>();
            world.Query(in qCandidates, (Entity e, ref Position p, ref Team t) =>
            {
                if (e.Id == ballEntity.Id)
                    return;
                if (!t.IsOffense)
                    return;

                var d = p.Value - pos;
                var distSq = d.LengthSquared();
                if (distSq > radiusSq)
                    return;

                if (distSq < bestDistSq - 0.0001f || (MathF.Abs(distSq - bestDistSq) <= 0.0001f && (bestId == 0 || e.Id < bestId)))
                {
                    bestId = e.Id;
                    bestDistSq = distSq;
                }
            });

            if (bestId != 0)
            {
                ball.State = TecmoSBGame.State.BallState.Held;
                ball.OwnerEntityId = bestId;
                ball.FlightKind = BallFlightKind.None;
                ball.Height = 0f;
                ball.IsComplete = true;
            }
            else
            {
                ball.State = TecmoSBGame.State.BallState.Dead;
                ball.OwnerEntityId = 0;
                ball.FlightKind = BallFlightKind.None;
                ball.Height = 0f;
                ball.IsComplete = true;
            }
        });
    }
}
