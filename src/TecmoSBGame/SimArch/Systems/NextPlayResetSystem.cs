using System;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.State;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Resets entities to a deterministic pre-snap state for a new play without recreating the world.
///
/// Trigger:
/// - when <see cref="PlayState.Phase"/> is <see cref="PlayPhase.PreSnap"/> and we haven't reset for this PlayId yet.
///
/// Responsibilities:
/// - zero velocities
/// - clear contact/interrupt state
/// - reset script components (FormationScript + PlayScript)
/// - place ball at MatchState.BallSpot (world X)
/// </summary>
public sealed class NextPlayResetSystem
{
    private int _lastResetPlayId;

    public void Update(World world, int ballEntityId, MatchState match, PlayState play)
    {
        if (play.Phase != PlayPhase.PreSnap)
            return;

        if (play.PlayId == _lastResetPlayId)
            return;

        _lastResetPlayId = play.PlayId;

        // Reset dynamic components.
        var qVel = new QueryDescription().WithAll<Velocity>();
        world.Query(in qVel, (Entity _, ref Velocity v) => v.Value = Vector2.Zero);

        var qBeh = new QueryDescription().WithAll<Behavior>();
        world.Query(in qBeh, (Entity _, ref Behavior b) =>
        {
            b.State = BehaviorState.Idle;
            b.TargetEntityId = -1;
            b.StateTimer = 0f;
        });

        var qStack = new QueryDescription().WithAll<BehaviorStack>();
        world.Query(in qStack, (Entity _, ref BehaviorStack s) => s = new BehaviorStack { Count = 0 });

        var qEng = new QueryDescription().WithAll<Engagement>();
        world.Query(in qEng, (Entity _, ref Engagement e) =>
        {
            e.PartnerEntityId = -1;
            e.CooldownSeconds = 0f;
        });

        var qBlocks = new QueryDescription().WithAll<BlockTarget>();
        world.Query(in qBlocks, (Entity _, ref BlockTarget b) =>
        {
            b.TargetEntityId = -1;
            b.IsEngaged = false;
            b.EngagedEntityId = -1;
            b.EngagementFrame = 0;
            b.IsDoubleTeam = false;
        });

        var qRush = new QueryDescription().WithAll<Rush>();
        world.Query(in qRush, (Entity _, ref Rush r) =>
        {
            r.HasLandmark = false;
            r.ReachedLandmark = false;
            r.Landmark = Vector2.Zero;
        });

        var qCov = new QueryDescription().WithAll<Coverage>();
        world.Query(in qCov, (Entity _, ref Coverage c) =>
        {
            c.InPursuit = false;
            c.PursuitTargetId = -1;
            c.ReactionDelay = 0;
            c.ReactionTimer = 0;
            c.HasReacted = false;
            c.LandmarkPosition = Vector2.Zero;
        });

        var qRoute = new QueryDescription().WithAll<RouteFollow>();
        world.Query(in qRoute, (Entity _, ref RouteFollow r) =>
        {
            r.NodeIndex = 0;
            r.FramesRemainingInNode = 0;
            r.HasAnchor = false;
            r.AnchorPosition = Vector2.Zero;
            r.Completed = false;
        });

        var qForm = new QueryDescription().WithAll<FormationScript>();
        world.Query(in qForm, (Entity _, ref FormationScript s) =>
        {
            s.Ip = 0;
            s.WaitSeconds = 0f;
            s.SuspendMovement = false;
        });

        var qPlay = new QueryDescription().WithAll<PlayScript>();
        world.Query(in qPlay, (Entity _, ref PlayScript s) =>
        {
            s.Ip = 0;
            s.WaitSeconds = 0f;
            s.PendingHandoffToEntityId = -1;
        });

        // Spot ball at the match ball spot (LOS).
        var startAbs = PlayState.ToAbsoluteYard(match.BallSpot, match.OffenseDirection);
        var losX = FieldMapping.AbsoluteYardToWorldX(startAbs);

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
            b.ElapsedSeconds = 0f;
            b.DurationSeconds = 0f;
            b.Height = 0f;
            b.IsComplete = false;

            p.Value = new Vector2(losX, p.Value.Y);
        });

        Console.WriteLine($"[sim-arch] reset-to-presnap play={play.PlayId} spotAbs={startAbs} down={match.Down} ytg={match.YardsToGo}");
    }
}
