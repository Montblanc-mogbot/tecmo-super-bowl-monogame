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
/// Minimal authoritative field goal / PAT flow for SimArch.
/// Handles snap/bootstrap, kick resolution, block/miss branches, and continuation
/// into either kickoff-after-score or normal possession change.
/// </summary>
public sealed class FieldGoalPlaySystem
{
    private const float FieldTop = 40f;
    private const float FieldBottom = 184f;
    private const float FieldLeft = 16f;
    private const float FieldRight = 240f;
    private const float BlockDistance = 10f;
    private const float CatchRadius = 14f;

    private int _preparedPlayId;
    private int _flightStartedPlayId;
    private int _resolvedFlightPlayId;
    private bool _attemptBlocked;

    public void UpdatePreMovement(World world, MatchState match, PlayState play, int ballEntityId, List<int> offenseIds, List<int> defenseIds, ref Control control)
    {
        if (!match.FieldGoalPending)
        {
            ResetState();
            return;
        }

        PrepareAttemptIfNeeded(world, match, play, ballEntityId, offenseIds, defenseIds);

        if (play.Phase != PlayPhase.InPlay)
            return;

        UpdateRush(world, ballEntityId);
        StartFlightIfNeeded(world, match, play, ballEntityId);
    }

    public void UpdatePostBall(World world, MatchState match, PlayState play, int ballEntityId, ref Control control)
    {
        if (!match.FieldGoalPending || play.Phase != PlayPhase.InPlay)
            return;

        ResolveKickIfNeeded(world, match, play, ballEntityId, ref control);
    }

    private void PrepareAttemptIfNeeded(World world, MatchState match, PlayState play, int ballEntityId, List<int> offenseIds, List<int> defenseIds)
    {
        if (_preparedPlayId == play.PlayId)
            return;

        _preparedPlayId = play.PlayId;
        _flightStartedPlayId = 0;
        _resolvedFlightPlayId = 0;
        _attemptBlocked = false;

        var kickingIds = GetPlayersForTeam(world, match.PossessionTeam);
        var defenseIdsLocal = GetPlayersForTeam(world, 1 - match.PossessionTeam);
        kickingIds.Sort();
        defenseIdsLocal.Sort();

        offenseIds.Clear();
        offenseIds.AddRange(kickingIds);
        defenseIds.Clear();
        defenseIds.AddRange(defenseIdsLocal);

        var direction = match.OffenseDirection;
        var signX = direction == OffenseDirection.LeftToRight ? 1f : -1f;
        var lineAbs = PlayState.ToAbsoluteYard(match.BallSpot, direction);
        var kickerAbs = lineAbs - (direction == OffenseDirection.LeftToRight ? 7 : -7);
        var holderAbs = lineAbs - (direction == OffenseDirection.LeftToRight ? 5 : -5);
        var lineX = FieldMapping.AbsoluteYardToWorldX(lineAbs);
        var kickYs = new[] { 112f, 112f, 84f, 140f, 70f, 154f, 58f, 166f, 96f, 128f, 112f };
        var rushYs = new[] { 112f, 80f, 144f, 64f, 160f, 96f, 128f, 52f, 172f, 88f, 136f };

        for (var i = 0; i < kickingIds.Count; i++)
        {
            var entityId = kickingIds[i];
            var xAbs = i == 0 ? kickerAbs : i == 1 ? holderAbs : lineAbs;
            var pos = new Vector2(FieldMapping.AbsoluteYardToWorldX((int)MathF.Round(xAbs)), kickYs[Math.Min(i, kickYs.Length - 1)]);
            SetPlayer(world, entityId, match.PossessionTeam, true, pos);
            WithEntity(world, entityId, e =>
            {
                e.RemoveIfPresent<FieldGoalBlockRush>();
                var beh = e.Get<Behavior>();
                beh.State = BehaviorState.Idle;
                beh.TargetPosition = pos;
                e.Set(beh);
            });
        }

        for (var i = 0; i < defenseIdsLocal.Count; i++)
        {
            var entityId = defenseIdsLocal[i];
            var pos = new Vector2(MathHelper.Clamp(lineX + (signX * 6f), 16f, 240f), rushYs[Math.Min(i, rushYs.Length - 1)]);
            SetPlayer(world, entityId, 1 - match.PossessionTeam, false, pos);
            WithEntity(world, entityId, e =>
            {
                var rush = FieldGoalBlockRush.Default;
                rush.DelayFrames = i == 0 ? 6 : 10 + (i % 3);
                rush.RushDirection = new Vector2(-signX, 0f);
                e.Upsert(rush);
            });
        }

        foreach (var id in kickingIds)
            SetHasBall(world, id, false);
        foreach (var id in defenseIdsLocal)
            SetHasBall(world, id, false);

        var holderId = kickingIds.Count > 1 ? kickingIds[1] : kickingIds.Count > 0 ? kickingIds[0] : -1;
        if (holderId > 0)
        {
            SetHasBall(world, holderId, true);
            SetBallHeld(world, ballEntityId, holderId);
        }

        play.StartAbsoluteYard = lineAbs;
        play.EndAbsoluteYard = lineAbs;
    }

    private void UpdateRush(World world, int ballEntityId)
    {
        _ = ballEntityId;
        var rush = new FieldGoalBlockRushSystem();
        rush.Update(world, 1f / 60f);
    }

    private void StartFlightIfNeeded(World world, MatchState match, PlayState play, int ballEntityId)
    {
        if (_flightStartedPlayId == play.PlayId)
            return;

        _flightStartedPlayId = play.PlayId;
        match.FieldGoalPlayActive = true;

        var holderId = GetHolderId(world, match.PossessionTeam);
        if (holderId <= 0)
            return;

        var start = GetPosition(world, holderId);
        var endAbs = match.FieldGoalTargetAbsoluteYardOverride ?? 100;
        var signX = match.OffenseDirection == OffenseDirection.LeftToRight ? 1f : -1f;
        var end = new Vector2(FieldMapping.AbsoluteYardToWorldX(endAbs), 112f);
        _attemptBlocked = match.ForceFieldGoalBlock || IsBlocked(world, ballEntityId, match);

        SetHasBall(world, holderId, false);

        if (_attemptBlocked)
        {
            var blockedAbs = Math.Clamp(play.StartAbsoluteYard + (match.OffenseDirection == OffenseDirection.LeftToRight ? -3 : 3), 0, 100);
            var blockedPos = ClampToField(new Vector2(FieldMapping.AbsoluteYardToWorldX(blockedAbs), start.Y));
            StartKickFlight(world, ballEntityId, start, blockedPos, durationSeconds: 0.35f, apexHeight: 5f);
            SetNearestDefenderTarget(world, match, blockedPos);
            return;
        }

        StartKickFlight(world, ballEntityId, start, end, durationSeconds: match.ExtraPointPending ? 0.7f : 0.95f, apexHeight: match.ExtraPointPending ? 16f : 20f);
        SetBallChaseTargets(world, match, end, signX);
    }

    private void ResolveKickIfNeeded(World world, MatchState match, PlayState play, int ballEntityId, ref Control control)
    {
        if (_resolvedFlightPlayId == play.PlayId)
            return;

        var ball = GetBall(world, ballEntityId);
        if (ball.FlightKind != BallFlightKind.Kickoff || !ball.IsComplete)
            return;

        _resolvedFlightPlayId = play.PlayId;

        var ballPos = GetPosition(world, ballEntityId);
        var ballAbs = FieldMapping.BallToAbsoluteYard(ballPos);
        play.EndAbsoluteYard = ballAbs;

        var yardsToGoal = Math.Max(0, 100 - play.StartAbsoluteYard);
        var isGood = !_attemptBlocked && !match.ForceFieldGoalMiss && IsKickGood(match, yardsToGoal);

        if (isGood)
        {
            SetBallDead(world, ballEntityId, ballPos);
            EndPlay(play, WhistleReason.Touchdown, match.FieldGoalTargetAbsoluteYardOverride ?? 100, 0, turnover: false, touchdown: true);
        }
        else if (_attemptBlocked)
        {
            var defenseIds = GetPlayersForTeam(world, 1 - match.PossessionTeam);
            defenseIds.Sort();
            var recovererId = FindNearest(world, defenseIds, ballPos, out var bestDistSq);
            if (recovererId > 0 && bestDistSq <= CatchRadius * CatchRadius)
            {
                SetBallHeld(world, ballEntityId, recovererId);
                SetHasBall(world, recovererId, true);
                ClearHasBallForOthers(world, defenseIds, recovererId);
                control.PendingForcedEntityId = recovererId;
            }
            else
            {
                SetBallDead(world, ballEntityId, ballPos);
            }

            EndPlay(play, WhistleReason.Turnover, ballAbs, 0, turnover: true, touchdown: false);
        }
        else
        {
            SetBallDead(world, ballEntityId, ballPos);
            EndPlay(play, WhistleReason.Turnover, play.StartAbsoluteYard, 0, turnover: true, touchdown: false);
        }
    }

    private static bool IsKickGood(MatchState match, int yardsToGoal)
    {
        var maxDistance = match.ExtraPointPending ? 20 : 55;
        return yardsToGoal <= maxDistance;
    }

    private static bool IsBlocked(World world, int ballEntityId, MatchState match)
    {
        var ballPos = GetPosition(world, ballEntityId);
        var defenseIds = GetPlayersForTeam(world, 1 - match.PossessionTeam);
        foreach (var entityId in defenseIds)
        {
            var pos = GetPosition(world, entityId);
            if (Vector2.DistanceSquared(pos, ballPos) <= BlockDistance * BlockDistance)
                return true;
        }

        return false;
    }

    private static void EndPlay(PlayState play, WhistleReason reason, int endAbsoluteYard, int yardsGained, bool turnover, bool touchdown)
    {
        var ended = new PlayEndedEvent(
            PlayId: play.PlayId,
            Reason: (int)reason,
            EndAbsoluteYard: endAbsoluteYard,
            YardsGained: yardsGained,
            Turnover: turnover,
            Touchdown: touchdown,
            Safety: false);
        SimEventBus.Send(ref ended);
    }

    private void ResetState()
    {
        _preparedPlayId = 0;
        _flightStartedPlayId = 0;
        _resolvedFlightPlayId = 0;
        _attemptBlocked = false;
    }

    private static List<int> GetPlayersForTeam(World world, int teamIndex)
    {
        var ids = new List<int>();
        var q = new QueryDescription().WithAll<Team>();
        world.Query(in q, (Entity e, ref Team team) =>
        {
            if (team.TeamIndex == teamIndex)
                ids.Add(e.Id);
        });
        return ids;
    }

    private static void SetPlayer(World world, int entityId, int teamIndex, bool isOffense, Vector2 position)
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
        });
    }

    private static int GetHolderId(World world, int teamIndex)
    {
        var ids = GetPlayersForTeam(world, teamIndex);
        ids.Sort();
        return ids.Count > 1 ? ids[1] : ids.Count > 0 ? ids[0] : -1;
    }

    private static void StartKickFlight(World world, int ballId, Vector2 start, Vector2 end, float durationSeconds, float apexHeight)
    {
        WithEntity(world, ballId, e =>
        {
            var ball = e.Get<Ball>();
            ball.State = BallState.Loose;
            ball.OwnerEntityId = -1;
            ball.FlightKind = BallFlightKind.Kickoff;
            ball.IsComplete = false;
            ball.StartPos = start;
            ball.EndPos = end;
            ball.ElapsedSeconds = 0f;
            ball.DurationSeconds = durationSeconds;
            ball.ApexHeight = apexHeight;
            ball.Height = 0f;
            e.Set(ball);

            if (e.Has<Position>())
            {
                var pos = e.Get<Position>();
                pos.Value = start;
                e.Set(pos);
            }

            if (e.Has<Velocity>())
            {
                var vel = e.Get<Velocity>();
                vel.Value = Vector2.Zero;
                e.Set(vel);
            }
        });
    }

    private static void SetBallHeld(World world, int ballId, int ownerId)
    {
        WithEntity(world, ballId, e =>
        {
            var ball = e.Get<Ball>();
            ball.State = BallState.Held;
            ball.OwnerEntityId = ownerId;
            ball.FlightKind = BallFlightKind.None;
            ball.IsComplete = false;
            ball.Height = 0f;
            e.Set(ball);
        });
        SetPosition(world, ballId, GetPosition(world, ownerId));
    }

    private static void SetBallDead(World world, int ballId, Vector2 position)
    {
        WithEntity(world, ballId, e =>
        {
            var ball = e.Get<Ball>();
            ball.State = BallState.Dead;
            ball.OwnerEntityId = -1;
            ball.FlightKind = BallFlightKind.None;
            ball.IsComplete = true;
            ball.Height = 0f;
            e.Set(ball);

            var pos = e.Get<Position>();
            pos.Value = position;
            e.Set(pos);

            if (e.Has<Velocity>())
            {
                var vel = e.Get<Velocity>();
                vel.Value = Vector2.Zero;
                e.Set(vel);
            }
        });
    }

    private static void SetHasBall(World world, int entityId, bool hasBall)
    {
        WithEntity(world, entityId, e =>
        {
            if (!e.Has<BallCarrier>())
                return;

            var carrier = e.Get<BallCarrier>();
            carrier.HasBall = hasBall;
            e.Set(carrier);
        });
    }

    private static void ClearHasBallForOthers(World world, List<int> entityIds, int exceptEntityId)
    {
        foreach (var id in entityIds)
        {
            if (id == exceptEntityId)
                continue;
            SetHasBall(world, id, false);
        }
    }

    private static void SetBallChaseTargets(World world, MatchState match, Vector2 ballTarget, float signX)
    {
        var defenseIds = GetPlayersForTeam(world, 1 - match.PossessionTeam);
        defenseIds.Sort();
        for (var i = 0; i < defenseIds.Count; i++)
        {
            var target = ClampToField(new Vector2(ballTarget.X - (signX * 6f), ballTarget.Y + ((i % 2 == 0 ? -1 : 1) * (4f + i))));
            WithEntity(world, defenseIds[i], e =>
            {
                if (!e.Has<Behavior>())
                    return;
                var beh = e.Get<Behavior>();
                beh.State = BehaviorState.MovingToPosition;
                beh.TargetPosition = target;
                e.Set(beh);
            });
        }
    }

    private static void SetNearestDefenderTarget(World world, MatchState match, Vector2 ballTarget)
    {
        var defenseIds = GetPlayersForTeam(world, 1 - match.PossessionTeam);
        defenseIds.Sort();
        foreach (var entityId in defenseIds)
        {
            WithEntity(world, entityId, e =>
            {
                if (!e.Has<Behavior>())
                    return;
                var beh = e.Get<Behavior>();
                beh.State = BehaviorState.MovingToPosition;
                beh.TargetPosition = ballTarget;
                e.Set(beh);
            });
        }
    }

    private static Ball GetBall(World world, int ballEntityId)
    {
        var found = false;
        var result = default(Ball);
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

    private static Vector2 ClampToField(Vector2 pos)
        => new(MathHelper.Clamp(pos.X, FieldLeft, FieldRight), MathHelper.Clamp(pos.Y, FieldTop, FieldBottom));

    private static int FindNearest(World world, List<int> entityIds, Vector2 at, out float bestDistSq)
    {
        bestDistSq = float.MaxValue;
        var bestId = -1;
        foreach (var entityId in entityIds)
        {
            var pos = GetPosition(world, entityId);
            var distSq = Vector2.DistanceSquared(pos, at);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestId = entityId;
            }
        }

        return bestId;
    }

    private static Vector2 GetPosition(World world, int entityId)
    {
        var found = false;
        var result = Vector2.Zero;
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

    private static void SetPosition(World world, int entityId, Vector2 position)
    {
        WithEntity(world, entityId, e =>
        {
            if (e.Has<Position>())
            {
                var pos = e.Get<Position>();
                pos.Value = position;
                e.Set(pos);
            }

            if (e.Has<Velocity>())
            {
                var vel = e.Get<Velocity>();
                vel.Value = Vector2.Zero;
                e.Set(vel);
            }
        });
    }

    private static void WithEntity(World world, int entityId, Action<Entity> action)
    {
        var q = new QueryDescription();
        world.Query(in q, (Entity e) =>
        {
            if (e.Id == entityId)
                action(e);
        });
    }
}
