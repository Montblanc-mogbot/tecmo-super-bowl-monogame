using System;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.State;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Post-play stabilization system.
///
/// Purpose:
/// - Ensure once a play is whistled (PlayPhase.PostPlay), entity state is stable for summary/render.
/// - Do NOT reset for the next play (see <see cref="NextPlayResetSystem"/>).
///
/// This avoids relying on world recreation.
/// </summary>
public sealed class PlayEndSystem
{
    private int _lastProcessedPlayId;

    public void Update(World world, int ballEntityId, MatchState match, PlayState play)
    {
        if (play.Phase != PlayPhase.PostPlay)
            return;

        if (play.PlayId == _lastProcessedPlayId)
            return;

        _lastProcessedPlayId = play.PlayId;

        // Freeze all velocities (deterministic) and set idle behaviors.
        var q = new QueryDescription().WithAll<Velocity>();
        world.Query(in q, (Entity _, ref Velocity v) => v.Value = Vector2.Zero);

        var qBeh = new QueryDescription().WithAll<Behavior>();
        world.Query(in qBeh, (Entity _, ref Behavior b) =>
        {
            b.State = BehaviorState.Idle;
            b.TargetEntityId = -1;
            b.StateTimer = 0f;
        });

        // Ensure ball is dead and placed at the end spot.
        var endX = FieldMapping.AbsoluteYardToWorldX(play.EndAbsoluteYard);
        var qBall = new QueryDescription().WithAll<Ball, Position>();
        world.Query(in qBall, (Entity e, ref Ball b, ref Position p) =>
        {
            if (e.Id != ballEntityId)
                return;

            b.State = BallState.Dead;
            b.OwnerEntityId = -1;
            b.FlightKind = BallFlightKind.None;
            b.PasserEntityId = 0;
            b.TargetEntityId = 0;
            b.IntendedReceiverRoleId = RoleId.Unknown;
            b.IntendedReceiverSlot = string.Empty;
            b.PassTargetPosition = Vector2.Zero;
            b.NearestDefenderEntityId = 0;
            b.NearestDefenderPosition = Vector2.Zero;
            b.ElapsedSeconds = 0f;
            b.DurationSeconds = 0f;
            b.Height = 0f;
            b.IsComplete = true;

            p.Value = new Vector2(endX, p.Value.Y);
        });

        Console.WriteLine($"[sim-arch] post-play stabilized play={play.PlayId} {match.Down}&{match.YardsToGo} endAbs={play.EndAbsoluteYard} gained={play.Result.YardsGained}");
    }
}
