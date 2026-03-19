using System;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Minimal PlayScript execution for Arch sim.
///
/// First milestone: supports a delayed QB->HB handoff (play_number=10 scaffolding).
/// </summary>
public sealed class PlayScriptSystem
{
    public void Update(World world, float dtSeconds, int ballEntityId, ref Control control)
    {
        // We can't capture a ref parameter inside the query lambda, so we hop through a local.
        var controlLocal = control;

        var query = new QueryDescription().WithAll<PlayScript>();

        world.Query(in query, (Entity e, ref PlayScript s) =>
        {
            if (s.WaitSeconds > 0f)
            {
                s.WaitSeconds = MathF.Max(0f, s.WaitSeconds - dtSeconds);
                if (s.WaitSeconds > 0f)
                    return;

                if (s.PendingHandoffToEntityId >= 0)
                {
                    ExecuteHandoff(world, ballEntityId, fromEntityId: e.Id, toEntityId: s.PendingHandoffToEntityId, ref controlLocal);
                    s.PendingHandoffToEntityId = -1;
                }
            }
        });

        control = controlLocal;
    }

    private static void ExecuteHandoff(World world, int ballEntityId, int fromEntityId, int toEntityId, ref Control control)
    {
        var ballQuery = new QueryDescription().WithAll<Ball>();
        var didUpdateBall = false;

        world.Query(in ballQuery, (Entity e, ref Ball b) =>
        {
            if (e.Id != ballEntityId)
                return;

            b.State = TecmoSBGame.State.BallState.Held;
            b.OwnerEntityId = toEntityId;
            b.FlightKind = BallFlightKind.None;
            b.ElapsedSeconds = 0f;
            b.DurationSeconds = 0f;
            b.Height = 0f;
            b.IsComplete = true;

            didUpdateBall = true;
        });

        if (!didUpdateBall)
            return;

        // Force control to the new carrier.
        control.PendingForcedEntityId = toEntityId;
        control.ControlledEntityId = toEntityId;

        Console.WriteLine($"[sim-arch] handoff from={fromEntityId} to={toEntityId}");

        // Give HB a deterministic run target so the play is visibly alive even before input wiring.
        // Give HB a deterministic run target so the play is visibly alive even before input wiring.
        var hbQuery = new QueryDescription().WithAll<Position, Behavior>();
        world.Query(in hbQuery, (Entity e, ref Position pos, ref Behavior beh) =>
        {
            if (e.Id != toEntityId)
                return;

            beh.State = BehaviorState.MovingToPosition;
            beh.TargetPosition = pos.Value + new Vector2(64, -8);
        });

        // Switch defenders to pursue ballcarrier.
        var query = new QueryDescription().WithAll<Team, Behavior>();
        world.Query(in query, (Entity e, ref Team t, ref Behavior beh) =>
        {
            if (t.IsOffense)
                return;

            beh.State = BehaviorState.TrackingEntity;
            beh.TargetEntityId = toEntityId;
        });
    }
}
