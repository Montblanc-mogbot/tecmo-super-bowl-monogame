using System;
using Arch.Core;
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
        var query = new QueryDescription().WithAll<PlayScript>();

        world.Query(in query, (Entity e, ref PlayScript s) =>
        {
            if (s.WaitSeconds > 0f)
            {
                s.WaitSeconds = MathF.Max(0f, s.WaitSeconds - dtSeconds);
                if (s.WaitSeconds > 0f)
                    return;

                if (s.PendingHandoffToEntityId != 0)
                {
                    ExecuteHandoff(world, ballEntityId, fromEntityId: e.Id, toEntityId: s.PendingHandoffToEntityId, ref control);
                    s.PendingHandoffToEntityId = 0;
                }
            }
        });
    }

    private static void ExecuteHandoff(World world, int ballEntityId, int fromEntityId, int toEntityId, ref Control control)
    {
        var ball = new Entity(world, ballEntityId);
        if (!ball.IsAlive() || !ball.Has<Ball>())
            return;

        var b = ball.Get<Ball>();
        b.State = TecmoSBGame.State.BallState.Held;
        b.OwnerEntityId = toEntityId;
        b.FlightKind = BallFlightKind.None;
        b.ElapsedSeconds = 0f;
        b.DurationSeconds = 0f;
        b.Height = 0f;
        b.IsComplete = true;
        ball.Set(b);

        // Force control to the new carrier.
        control.PendingForcedEntityId = toEntityId;
        control.ControlledEntityId = toEntityId;

        Console.WriteLine($"[sim-arch] handoff from={fromEntityId} to={toEntityId}");

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
