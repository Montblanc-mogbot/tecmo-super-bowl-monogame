using System;
using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.Events;
using TecmoSBGame.SimArch.State;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Minimal authoritative kickoff flow for SimArch.
///
/// Handles kickoff setup, kick flight, catch/loose recovery handoff, touchbacks,
/// basic return/covearge steering, and post-kickoff possession bootstrap.
/// </summary>
public sealed class KickoffPlaySystem
{
    private const float CatchRadius = 14f;
    private const float FieldTop = 40f;
    private const float FieldBottom = 184f;
    private const float FieldLeft = 16f;
    private const float FieldRight = 240f;

    private int _preparedPlayId;
    private int _flightStartedPlayId;
    private int _resolvedFlightPlayId;

    public void UpdatePreMovement(World world, MatchState match, PlayState play, int ballEntityId, List<int> offenseIds, List<int> defenseIds, ref Control control)
    {
        if (!match.KickoffPending)
        {
            ResetState();
            return;
        }

        PrepareKickoffIfNeeded(world, match, play, ballEntityId, offenseIds, defenseIds);

        if (play.Phase != PlayPhase.InPlay)
            return;

        StartFlightIfNeeded(world, match, play, ballEntityId);
        UpdateCoverage(world, match, ballEntityId);
        UpdateReturn(world, match, control.ControlledEntityId);
        UpdateReturnSupport(world, match, ballEntityId);
    }

    public void UpdatePostBall(World world, MatchState match, PlayState play, int ballEntityId, ref Control control)
    {
        if (!match.KickoffPending || play.Phase != PlayPhase.InPlay)
            return;

        ResolveFlightIfNeeded(world, match, play, ballEntityId, ref control);
    }

    private void PrepareKickoffIfNeeded(World world, MatchState match, PlayState play, int ballEntityId, List<int> offenseIds, List<int> defenseIds)
    {
        if (_preparedPlayId == play.PlayId)
            return;

        _preparedPlayId = play.PlayId;
        _flightStartedPlayId = 0;
        _resolvedFlightPlayId = 0;

        var receivingIds = GetPlayersForTeam(world, match.ReceivingTeamIndex);
        var kickingIds = GetPlayersForTeam(world, match.KickingTeamIndex);
        receivingIds.Sort();
        kickingIds.Sort();

        var receivingDirection = GetReceivingDirection(match);
        var returnAbs = receivingDirection == OffenseDirection.LeftToRight ? 8 : 92;
        var frontAbs = receivingDirection == OffenseDirection.LeftToRight ? 20 : 80;
        var secondLineAbs = receivingDirection == OffenseDirection.LeftToRight ? 28 : 72;
        var kickAbs = receivingDirection == OffenseDirection.LeftToRight ? 65 : 35;
        var coverageAbs = receivingDirection == OffenseDirection.LeftToRight ? 58 : 42;
        var signX = receivingDirection == OffenseDirection.LeftToRight ? 1f : -1f;

        offenseIds.Clear();
        offenseIds.AddRange(receivingIds);
        defenseIds.Clear();
        defenseIds.AddRange(kickingIds);

        var receivingYs = new[] { 112f, 72f, 152f, 56f, 88f, 136f, 168f, 64f, 96f, 128f, 160f };
        var frontYs = new[] { 52f, 76f, 100f, 124f, 148f, 172f, 64f, 88f, 136f, 160f };
        var coverageYs = new[] { 44f, 58f, 72f, 86f, 100f, 124f, 138f, 152f, 166f, 180f, 112f };

        for (var i = 0; i < receivingIds.Count; i++)
        {
            var entityId = receivingIds[i];
            var posAbs = i == 0 ? returnAbs : (i <= 5 ? frontAbs : secondLineAbs);
            var y = receivingYs[Math.Min(i, receivingYs.Length - 1)];
            SetPlayerForKickoff(world, entityId, match.ReceivingTeamIndex, isOffense: true, new Vector2(FieldMapping.AbsoluteYardToWorldX(posAbs), y));

            WithEntity(world, entityId, e =>
            {
                if (i == 0)
                    e.Upsert(KickoffReturn.Default);
                else
                    e.RemoveIfPresent<KickoffReturn>();

                e.RemoveIfPresent<KickoffCoverage>();
            });
        }

        for (var i = 0; i < kickingIds.Count; i++)
        {
            var entityId = kickingIds[i];
            var isKicker = i == 0;
            var xAbs = isKicker ? kickAbs : coverageAbs;
            var y = coverageYs[Math.Min(i, coverageYs.Length - 1)];
            SetPlayerForKickoff(world, entityId, match.KickingTeamIndex, isOffense: false, new Vector2(FieldMapping.AbsoluteYardToWorldX(xAbs), y));

            var laneLandmarkAbs = coverageAbs + (signX * 14f);
            var laneLandmark = new Vector2(
                MathHelper.Clamp(FieldMapping.AbsoluteYardToWorldX((int)MathF.Round(laneLandmarkAbs)), FieldLeft, FieldRight),
                MathHelper.Clamp(y + (isKicker ? 0f : (i % 2 == 0 ? -6f : 6f)), FieldTop, FieldBottom));

            WithEntity(world, entityId, e =>
            {
                e.RemoveIfPresent<KickoffReturn>();
                e.Upsert(new KickoffCoverage
                {
                    LaneIndex = i,
                    LaneCount = Math.Max(1, kickingIds.Count),
                    IsContain = i == 0 || i == kickingIds.Count - 1,
                    LaneLandmark = laneLandmark,
                    ReturnerEntityId = receivingIds.Count > 0 ? receivingIds[0] : -1,
                    BreakOnReturner = false,
                });
            });
        }

        foreach (var id in receivingIds)
            SetHasBall(world, id, false);
        foreach (var id in kickingIds)
            SetHasBall(world, id, false);

        if (kickingIds.Count > 0)
        {
            var kickerId = kickingIds[0];
            SetHasBall(world, kickerId, true);
            SetBallHeld(world, ballEntityId, kickerId);
        }

        play.StartAbsoluteYard = GetTouchbackAbsoluteYard(match.ReceivingTeamIndex);
        play.EndAbsoluteYard = play.StartAbsoluteYard;
    }

    private void StartFlightIfNeeded(World world, MatchState match, PlayState play, int ballEntityId)
    {
        if (_flightStartedPlayId == play.PlayId)
            return;

        _flightStartedPlayId = play.PlayId;
        match.KickoffPlayActive = true;
        match.PossessionTeam = match.ReceivingTeamIndex;
        match.OffenseDirection = GetReceivingDirection(match);

        var kickingIds = GetPlayersForTeam(world, match.KickingTeamIndex);
        kickingIds.Sort();
        if (kickingIds.Count <= 0)
            return;

        var kickerId = kickingIds[0];
        var start = GetPosition(world, kickerId);
        var defaultLandingAbs = match.ReceivingTeamIndex == 0 ? 8 : 92;
        var landingAbs = match.KickoffLandingAbsoluteYardOverride ?? defaultLandingAbs;
        var end = new Vector2(FieldMapping.AbsoluteYardToWorldX(landingAbs), 112f);
        var signX = match.ReceivingTeamIndex == 0 ? 1f : -1f;

        SetHasBall(world, kickerId, false);
        KickoffFlightStartSystem.StartKickoff(world, ballEntityId, start, end, durationSeconds: 1.45f, apexHeight: 18f);

        var receivingIds = GetPlayersForTeam(world, match.ReceivingTeamIndex);
        receivingIds.Sort();
        for (var i = 0; i < receivingIds.Count; i++)
        {
            var entityId = receivingIds[i];
            var target = i == 0
                ? end
                : new Vector2(end.X + (signX * 10f), MathHelper.Clamp(end.Y + ((i % 2 == 0 ? -1 : 1) * (8f + i)), FieldTop, FieldBottom));
            WithEntity(world, entityId, e =>
            {
                var beh = e.Get<Behavior>();
                beh.State = BehaviorState.MovingToPosition;
                beh.TargetPosition = ClampToField(target);
                e.Set(beh);
            });
        }
    }

    private void ResolveFlightIfNeeded(World world, MatchState match, PlayState play, int ballEntityId, ref Control control)
    {
        if (_resolvedFlightPlayId == play.PlayId)
            return;

        var ball = GetBall(world, ballEntityId);
        if (ball.FlightKind != BallFlightKind.Kickoff || !ball.IsComplete)
            return;

        _resolvedFlightPlayId = play.PlayId;

        var landingPos = GetPosition(world, ballEntityId);
        var landingAbs = FieldMapping.BallToAbsoluteYard(landingPos);
        play.StartAbsoluteYard = landingAbs;
        play.EndAbsoluteYard = landingAbs;

        if (IsTouchback(match.ReceivingTeamIndex, landingAbs))
        {
            SetBallDead(world, ballEntityId, landingPos);
            var whistle = new WhistleEvent("touchback");
            SimEventBus.Send(ref whistle);
            var ended = new PlayEndedEvent(
                PlayId: play.PlayId,
                Reason: (int)WhistleReason.Touchback,
                EndAbsoluteYard: GetTouchbackAbsoluteYard(match.ReceivingTeamIndex),
                YardsGained: 0,
                Turnover: false,
                Touchdown: false,
                Safety: false);
            SimEventBus.Send(ref ended);
            return;
        }

        var receivingIds = GetPlayersForTeam(world, match.ReceivingTeamIndex);
        receivingIds.Sort();
        var chosenId = FindNearest(world, receivingIds, landingPos, out var bestDistSq);

        if (chosenId > 0 && bestDistSq <= CatchRadius * CatchRadius)
        {
            SetBallHeld(world, ballEntityId, chosenId);
            SetHasBall(world, chosenId, true);
            ClearHasBallForOthers(world, receivingIds, chosenId);
            control.PendingForcedEntityId = chosenId;
            var caught = new BallCaughtEvent(chosenId, landingPos);
            SimEventBus.Send(ref caught);
            return;
        }

        SetBallLoose(world, ballEntityId, landingPos);
        ClearHasBallForOthers(world, receivingIds, chosenId: -1);
    }

    private void UpdateCoverage(World world, MatchState match, int ballEntityId)
    {
        var ball = GetBall(world, ballEntityId);
        var returnerId = ball.State == BallState.Held ? ball.OwnerEntityId : -1;
        var returnerPos = returnerId > 0 ? GetPosition(world, returnerId) : ball.EndPos;

        var ids = GetEntitiesWith<KickoffCoverage>(world);
        foreach (var entityId in ids)
        {
            WithEntity(world, entityId, e =>
            {
                var cov = e.Get<KickoffCoverage>();
                var beh = e.Get<Behavior>();
                cov.ReturnerEntityId = returnerId;
                cov.BreakOnReturner = returnerId > 0;

                var target = cov.BreakOnReturner
                    ? ComputeCoverageTarget(world, entityId, cov, returnerPos)
                    : cov.LaneLandmark;

                beh.State = BehaviorState.MovingToPosition;
                beh.TargetPosition = ClampToField(target);
                e.Set(cov);
                e.Set(beh);
            });
        }
    }

    private void UpdateReturn(World world, MatchState match, int controlledEntityId)
    {
        var returners = GetEntitiesWith<KickoffReturn>(world);
        foreach (var entityId in returners)
        {
            if (entityId == controlledEntityId)
                continue;
            if (!HasBall(world, entityId))
                continue;

            WithEntity(world, entityId, e =>
            {
                var kr = e.Get<KickoffReturn>();
                var beh = e.Get<Behavior>();
                var pos = e.Get<Position>().Value;
                var signX = match.OffenseDirection == OffenseDirection.LeftToRight ? 1f : -1f;

                if (kr.LaneLockFrames > 0)
                {
                    kr.LaneLockFrames--;
                }
                else
                {
                    var lookAheadX = pos.X + (44f * signX);
                    var left = new Vector2(lookAheadX, pos.Y - 28f);
                    var center = new Vector2(lookAheadX, pos.Y);
                    var right = new Vector2(lookAheadX, pos.Y + 28f);

                    var sLeft = ScoreLane(world, entityId, pos, left, signX);
                    var sCenter = ScoreLane(world, entityId, pos, center, signX);
                    var sRight = ScoreLane(world, entityId, pos, right, signX);

                    if (sLeft <= sCenter && sLeft <= sRight)
                    {
                        kr.Lane = KickoffReturnLane.Left;
                        kr.LastChosenTarget = left;
                    }
                    else if (sCenter <= sRight)
                    {
                        kr.Lane = KickoffReturnLane.Center;
                        kr.LastChosenTarget = center;
                    }
                    else
                    {
                        kr.Lane = KickoffReturnLane.Right;
                        kr.LastChosenTarget = right;
                    }

                    kr.LaneLockFrames = 20;
                }

                beh.State = BehaviorState.MovingToPosition;
                beh.TargetPosition = ClampToField(kr.LastChosenTarget);
                e.Set(kr);
                e.Set(beh);
            });
        }
    }

    private void UpdateReturnSupport(World world, MatchState match, int ballEntityId)
    {
        var ball = GetBall(world, ballEntityId);
        if (ball.State != BallState.Held || ball.OwnerEntityId <= 0)
            return;

        var returnerId = ball.OwnerEntityId;
        var returnerPos = GetPosition(world, returnerId);
        var signX = match.OffenseDirection == OffenseDirection.LeftToRight ? 1f : -1f;
        var offenseIds = GetPlayersForTeam(world, match.ReceivingTeamIndex);
        offenseIds.Sort();

        var slot = 0;
        foreach (var entityId in offenseIds)
        {
            if (entityId == returnerId)
                continue;

            var laneOffset = (slot % 5) - 2;
            var depth = slot < 4 ? 16f : 28f;
            var target = new Vector2(
                returnerPos.X + (depth * signX),
                returnerPos.Y + (laneOffset * 12f));
            slot++;

            WithEntity(world, entityId, e =>
            {
                var beh = e.Get<Behavior>();
                beh.State = BehaviorState.MovingToPosition;
                beh.TargetPosition = ClampToField(target);
                e.Set(beh);
            });
        }
    }

    private static Vector2 ComputeCoverageTarget(World world, int defenderId, KickoffCoverage coverage, Vector2 returnerPos)
    {
        if (!coverage.IsContain)
            return returnerPos;

        var myPos = GetPosition(world, defenderId);
        var centerY = (FieldTop + FieldBottom) * 0.5f;
        var side = returnerPos.Y < centerY ? -1f : 1f;
        var offsetY = 8f * side;
        var x = MathHelper.Lerp(myPos.X, returnerPos.X, 0.65f);
        return new Vector2(x, returnerPos.Y + offsetY);
    }

    private static int ScoreLane(World world, int returnerId, Vector2 returnerPos, Vector2 laneTarget, float signX)
    {
        var score = 0;
        var defenders = GetEntitiesWith<KickoffCoverage>(world);
        foreach (var defenderId in defenders)
        {
            var p = GetPosition(world, defenderId);
            if (MathF.Abs(p.X - laneTarget.X) > 35f || MathF.Abs(p.Y - laneTarget.Y) > 22f)
                continue;

            var ahead = (p.X - returnerPos.X) * signX > -2f;
            score += ahead ? 2 : 1;
        }

        return score;
    }

    private static void SetPlayerForKickoff(World world, int entityId, int teamIndex, bool isOffense, Vector2 position)
    {
        WithEntity(world, entityId, e =>
        {
            var team = e.Get<Team>();
            team.TeamIndex = teamIndex;
            team.IsOffense = isOffense;
            e.Set(team);

            var pos = e.Get<Position>();
            pos.Value = position;
            e.Set(pos);

            if (e.Has<Velocity>())
            {
                var vel = e.Get<Velocity>();
                vel.Value = Vector2.Zero;
                e.Set(vel);
            }

            var beh = e.Get<Behavior>();
            beh.State = BehaviorState.Idle;
            beh.TargetEntityId = -1;
            beh.TargetPosition = position;
            beh.StateTimer = 0f;
            e.Set(beh);
        });
    }

    private static List<int> GetPlayersForTeam(World world, int teamIndex)
    {
        var ids = new List<int>();
        var q = new QueryDescription().WithAll<Team, Position>();
        world.Query(in q, (Entity e, ref Team team, ref Position _) =>
        {
            if (team.TeamIndex == teamIndex)
                ids.Add(e.Id);
        });
        return ids;
    }

    private static List<int> GetEntitiesWith<T>(World world) where T : unmanaged
    {
        var ids = new List<int>();
        var q = new QueryDescription().WithAll<T>();
        world.Query(in q, (Entity e, ref T _) => ids.Add(e.Id));
        return ids;
    }

    private static int FindNearest(World world, IReadOnlyList<int> ids, Vector2 position, out float bestDistSq)
    {
        var bestId = -1;
        bestDistSq = float.PositiveInfinity;
        for (var i = 0; i < ids.Count; i++)
        {
            var candidate = ids[i];
            var d = Vector2.DistanceSquared(GetPosition(world, candidate), position) + (candidate * 0.0001f);
            if (d < bestDistSq)
            {
                bestDistSq = d;
                bestId = candidate;
            }
        }

        return bestId;
    }

    private static void ClearHasBallForOthers(World world, IReadOnlyList<int> ids, int chosenId)
    {
        for (var i = 0; i < ids.Count; i++)
            SetHasBall(world, ids[i], ids[i] == chosenId);
    }

    private static bool HasBall(World world, int entityId)
    {
        var hasBall = false;
        WithEntity(world, entityId, e => hasBall = e.Get<BallCarrier>().HasBall);
        return hasBall;
    }

    private static void SetHasBall(World world, int entityId, bool hasBall)
    {
        WithEntity(world, entityId, e =>
        {
            var carrier = e.Get<BallCarrier>();
            carrier.HasBall = hasBall;
            e.Set(carrier);
        });
    }

    private static void SetBallHeld(World world, int ballEntityId, int ownerId)
    {
        var ownerPos = GetPosition(world, ownerId);
        WithBall(world, ballEntityId, (ref Ball ball, ref Position pos, ref Velocity vel) =>
        {
            ball.State = BallState.Held;
            ball.OwnerEntityId = ownerId;
            ball.FlightKind = BallFlightKind.None;
            ball.IsComplete = true;
            ball.Height = 0f;
            ball.ElapsedSeconds = 0f;
            ball.DurationSeconds = 0f;
            pos.Value = ownerPos;
            vel.Value = Vector2.Zero;
        });
    }

    private static void SetBallLoose(World world, int ballEntityId, Vector2 at)
    {
        WithBall(world, ballEntityId, (ref Ball ball, ref Position pos, ref Velocity vel) =>
        {
            ball.State = BallState.Loose;
            ball.OwnerEntityId = -1;
            ball.FlightKind = BallFlightKind.None;
            ball.IsComplete = true;
            ball.Height = 0f;
            ball.ElapsedSeconds = 0f;
            ball.DurationSeconds = 0f;
            pos.Value = at;
            vel.Value = Vector2.Zero;
        });
    }

    private static void SetBallDead(World world, int ballEntityId, Vector2 at)
    {
        WithBall(world, ballEntityId, (ref Ball ball, ref Position pos, ref Velocity vel) =>
        {
            ball.State = BallState.Dead;
            ball.OwnerEntityId = -1;
            ball.FlightKind = BallFlightKind.None;
            ball.IsComplete = true;
            ball.Height = 0f;
            ball.ElapsedSeconds = 0f;
            ball.DurationSeconds = 0f;
            pos.Value = at;
            vel.Value = Vector2.Zero;
        });
    }

    private static Ball GetBall(World world, int ballEntityId)
    {
        var result = default(Ball);
        var found = false;
        var q = new QueryDescription().WithAll<Ball>();
        world.Query(in q, (Entity e, ref Ball ball) =>
        {
            if (found || e.Id != ballEntityId)
                return;
            result = ball;
            found = true;
        });
        return result;
    }

    private static Vector2 GetPosition(World world, int entityId)
    {
        var result = Vector2.Zero;
        var found = false;
        var q = new QueryDescription().WithAll<Position>();
        world.Query(in q, (Entity e, ref Position pos) =>
        {
            if (found || e.Id != entityId)
                return;
            result = pos.Value;
            found = true;
        });
        return result;
    }

    private static void WithBall(World world, int ballEntityId, BallMutation action)
    {
        var q = new QueryDescription().WithAll<Ball, Position, Velocity>();
        world.Query(in q, (Entity e, ref Ball ball, ref Position pos, ref Velocity vel) =>
        {
            if (e.Id != ballEntityId)
                return;
            action(ref ball, ref pos, ref vel);
        });
    }

    private static void WithEntity(World world, int entityId, Action<Entity> action)
    {
        var handled = false;
        var q = new QueryDescription().WithAll<Position>();
        world.Query(in q, (Entity e, ref Position _) =>
        {
            if (handled || e.Id != entityId)
                return;
            action(e);
            handled = true;
        });
    }

    private static OffenseDirection GetReceivingDirection(MatchState match)
        => match.ReceivingTeamIndex == 0 ? OffenseDirection.LeftToRight : OffenseDirection.RightToLeft;

    private static int GetTouchbackAbsoluteYard(int receivingTeam)
        => receivingTeam == 0 ? MatchState.TouchbackSpotYard : 100 - MatchState.TouchbackSpotYard;

    private static bool IsTouchback(int receivingTeam, int absoluteYard)
        => receivingTeam == 0 ? absoluteYard <= 0 : absoluteYard >= 100;

    private static Vector2 ClampToField(Vector2 p)
        => new(
            MathHelper.Clamp(p.X, FieldLeft, FieldRight),
            MathHelper.Clamp(p.Y, FieldTop, FieldBottom));

    private void ResetState()
    {
        _preparedPlayId = 0;
        _flightStartedPlayId = 0;
        _resolvedFlightPlayId = 0;
    }

    private delegate void BallMutation(ref Ball ball, ref Position position, ref Velocity velocity);
}
