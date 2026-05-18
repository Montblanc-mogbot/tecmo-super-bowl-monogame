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
/// Minimal authoritative punt flow for SimArch.
/// Handles snap/bootstrap, punt flight, coverage/return steering, muff+recovery,
/// downing, touchbacks, and possession transition.
/// </summary>
public sealed class PuntPlaySystem
{
    private const float CatchRadius = 14f;
    private const float DowningRadius = 10f;
    private const float FieldTop = 40f;
    private const float FieldBottom = 184f;
    private const float FieldLeft = 16f;
    private const float FieldRight = 240f;

    private int _preparedPlayId;
    private int _flightStartedPlayId;
    private int _resolvedFlightPlayId;

    public void UpdatePreMovement(World world, MatchState match, PlayState play, int ballEntityId, List<int> offenseIds, List<int> defenseIds, ref Control control)
    {
        if (!match.PuntPending)
        {
            ResetState();
            return;
        }

        PreparePuntIfNeeded(world, match, play, ballEntityId, offenseIds, defenseIds);

        if (play.Phase != PlayPhase.InPlay)
            return;

        StartFlightIfNeeded(world, match, play, ballEntityId);
        UpdateCoverage(world, match, ballEntityId);
        UpdateReturn(world, match, control.ControlledEntityId);
        UpdateReturnSupport(world, match, ballEntityId);
        TryDownLooseBall(world, match, play, ballEntityId);
    }

    public void UpdatePostBall(World world, MatchState match, PlayState play, int ballEntityId, ref Control control)
    {
        if (!match.PuntPending || play.Phase != PlayPhase.InPlay)
            return;

        ResolveFlightIfNeeded(world, match, play, ballEntityId, ref control);
        TryResolveLooseBall(world, match, play, ballEntityId, ref control);
    }

    private void PreparePuntIfNeeded(World world, MatchState match, PlayState play, int ballEntityId, List<int> offenseIds, List<int> defenseIds)
    {
        if (_preparedPlayId == play.PlayId)
            return;

        _preparedPlayId = play.PlayId;
        _flightStartedPlayId = 0;
        _resolvedFlightPlayId = 0;

        var puntingIds = GetPlayersForTeam(world, match.PossessionTeam);
        var returnIds = GetPlayersForTeam(world, 1 - match.PossessionTeam);
        puntingIds.Sort();
        returnIds.Sort();

        offenseIds.Clear();
        offenseIds.AddRange(puntingIds);
        defenseIds.Clear();
        defenseIds.AddRange(returnIds);

        var direction = match.OffenseDirection;
        var signX = direction == OffenseDirection.LeftToRight ? 1f : -1f;
        var lineAbs = PlayState.ToAbsoluteYard(match.BallSpot, match.OffenseDirection);
        var punterAbs = lineAbs - (direction == OffenseDirection.LeftToRight ? 6 : -6);
        var protectAbs = lineAbs - (direction == OffenseDirection.LeftToRight ? 1 : -1);
        var returnerAbs = lineAbs + (direction == OffenseDirection.LeftToRight ? 34 : -34);
        var midReturnAbs = lineAbs + (direction == OffenseDirection.LeftToRight ? 22 : -22);
        var coverageAbs = lineAbs + (direction == OffenseDirection.LeftToRight ? 4 : -4);

        var puntYs = new[] { 112f, 72f, 152f, 60f, 88f, 136f, 164f, 52f, 100f, 124f, 172f };
        var returnYs = new[] { 112f, 66f, 158f, 50f, 82f, 142f, 174f, 94f, 126f, 70f, 150f };

        for (var i = 0; i < puntingIds.Count; i++)
        {
            var entityId = puntingIds[i];
            var abs = i == 0 ? punterAbs : (i <= 6 ? protectAbs : coverageAbs);
            var pos = new Vector2(FieldMapping.AbsoluteYardToWorldX((int)MathF.Round(abs)), puntYs[Math.Min(i, puntYs.Length - 1)]);
            SetPlayerForSpecialTeams(world, entityId, match.PossessionTeam, isOffense: false, pos);
            WithEntity(world, entityId, e =>
            {
                e.RemoveIfPresent<PuntReturn>();
                e.Upsert(new PuntCoverage
                {
                    LaneIndex = i,
                    LaneCount = Math.Max(1, puntingIds.Count),
                    IsGunner = i == 1 || i == puntingIds.Count - 2,
                    LaneLandmark = ClampToField(new Vector2(pos.X + (signX * 18f), pos.Y + (i % 2 == 0 ? -8f : 8f))),
                    ReturnerEntityId = returnIds.Count > 0 ? returnIds[0] : -1,
                    BreakOnReturner = false,
                });
            });
        }

        for (var i = 0; i < returnIds.Count; i++)
        {
            var entityId = returnIds[i];
            var abs = i == 0 ? returnerAbs : midReturnAbs;
            var pos = new Vector2(FieldMapping.AbsoluteYardToWorldX((int)MathF.Round(abs)), returnYs[Math.Min(i, returnYs.Length - 1)]);
            SetPlayerForSpecialTeams(world, entityId, 1 - match.PossessionTeam, isOffense: true, pos);
            WithEntity(world, entityId, e =>
            {
                e.RemoveIfPresent<PuntCoverage>();
                if (i == 0)
                    e.Upsert(PuntReturn.Default);
                else
                    e.RemoveIfPresent<PuntReturn>();
            });
        }

        foreach (var id in puntingIds)
            SetHasBall(world, id, false);
        foreach (var id in returnIds)
            SetHasBall(world, id, false);

        if (puntingIds.Count > 0)
        {
            SetHasBall(world, puntingIds[0], true);
            SetBallHeld(world, ballEntityId, puntingIds[0]);
        }

        play.StartAbsoluteYard = lineAbs;
        play.EndAbsoluteYard = lineAbs;
    }

    private void StartFlightIfNeeded(World world, MatchState match, PlayState play, int ballEntityId)
    {
        if (_flightStartedPlayId == play.PlayId)
            return;

        _flightStartedPlayId = play.PlayId;
        match.PuntPlayActive = true;

        var puntingIds = GetPlayersForTeam(world, match.PossessionTeam);
        puntingIds.Sort();
        if (puntingIds.Count <= 0)
            return;

        var punterId = puntingIds[0];
        var start = GetPosition(world, punterId);
        var defaultLandingAbs = ComputeDefaultLandingAbsoluteYard(match);
        var landingAbs = match.PuntLandingAbsoluteYardOverride ?? defaultLandingAbs;
        var end = new Vector2(FieldMapping.AbsoluteYardToWorldX(landingAbs), 112f);

        SetHasBall(world, punterId, false);
        StartPuntFlight(world, ballEntityId, start, end, 1.55f, 20f);
    }

    private void ResolveFlightIfNeeded(World world, MatchState match, PlayState play, int ballEntityId, ref Control control)
    {
        if (_resolvedFlightPlayId == play.PlayId)
            return;

        var ball = GetBall(world, ballEntityId);
        if (ball.FlightKind != BallFlightKind.Punt || !ball.IsComplete)
            return;

        _resolvedFlightPlayId = play.PlayId;

        var landingPos = GetPosition(world, ballEntityId);
        var landingAbs = FieldMapping.BallToAbsoluteYard(landingPos);
        play.StartAbsoluteYard = landingAbs;
        play.EndAbsoluteYard = landingAbs;

        if (IsTouchback(match.OffenseDirection, landingAbs))
        {
            SetBallDead(world, ballEntityId, landingPos);
            EndPlay(play, WhistleReason.Touchback, GetTouchbackAbsoluteYard(1 - match.PossessionTeam), 0, turnover: true);
            return;
        }

        var returnIds = GetPlayersForTeam(world, 1 - match.PossessionTeam);
        returnIds.Sort();
        var chosenId = FindNearest(world, returnIds, landingPos, out var bestDistSq);

        if (match.ForcePuntMuff && chosenId > 0)
        {
            match.ForcePuntMuff = false;
            SetBallLoose(world, ballEntityId, landingPos);
            ClearHasBallForOthers(world, returnIds, -1);
            return;
        }

        if (chosenId > 0 && bestDistSq <= CatchRadius * CatchRadius)
        {
            SetBallHeld(world, ballEntityId, chosenId);
            SetHasBall(world, chosenId, true);
            ClearHasBallForOthers(world, returnIds, chosenId);
            control.PendingForcedEntityId = chosenId;
            var caught = new BallCaughtEvent(chosenId, landingPos);
            SimEventBus.Send(ref caught);
            return;
        }

        SetBallLoose(world, ballEntityId, landingPos);
        ClearHasBallForOthers(world, returnIds, -1);
    }

    private void TryResolveLooseBall(World world, MatchState match, PlayState play, int ballEntityId, ref Control control)
    {
        var ball = GetBall(world, ballEntityId);
        if (ball.State != BallState.Loose)
            return;

        var at = GetPosition(world, ballEntityId);
        var returnIds = GetPlayersForTeam(world, 1 - match.PossessionTeam);
        var puntIds = GetPlayersForTeam(world, match.PossessionTeam);
        var returnerId = FindNearest(world, returnIds, at, out var returnDistSq);
        var coverageId = FindNearest(world, puntIds, at, out var coverageDistSq);

        if (returnerId > 0 && returnDistSq <= CatchRadius * CatchRadius)
        {
            SetBallHeld(world, ballEntityId, returnerId);
            SetHasBall(world, returnerId, true);
            ClearHasBallForOthers(world, returnIds, returnerId);
            control.PendingForcedEntityId = returnerId;
            return;
        }

        if (coverageId > 0 && coverageDistSq <= DowningRadius * DowningRadius)
        {
            SetBallDead(world, ballEntityId, at);
            EndPlay(play, WhistleReason.Turnover, FieldMapping.BallToAbsoluteYard(at), 0, turnover: true);
        }
    }

    private void TryDownLooseBall(World world, MatchState match, PlayState play, int ballEntityId)
    {
        var ball = GetBall(world, ballEntityId);
        if (ball.State != BallState.Loose || play.IsOver)
            return;

        var at = GetPosition(world, ballEntityId);
        var abs = FieldMapping.BallToAbsoluteYard(at);
        if (IsTouchback(match.OffenseDirection, abs))
        {
            SetBallDead(world, ballEntityId, at);
            EndPlay(play, WhistleReason.Touchback, GetTouchbackAbsoluteYard(1 - match.PossessionTeam), 0, turnover: true);
        }
    }

    private void UpdateCoverage(World world, MatchState match, int ballEntityId)
    {
        var ball = GetBall(world, ballEntityId);
        var returnerId = ball.State == BallState.Held ? ball.OwnerEntityId : -1;
        var targetPos = returnerId > 0 ? GetPosition(world, returnerId) : GetPosition(world, ballEntityId);

        foreach (var entityId in GetEntitiesWith<PuntCoverage>(world))
        {
            WithEntity(world, entityId, e =>
            {
                var cov = e.Get<PuntCoverage>();
                var beh = e.Get<Behavior>();
                cov.ReturnerEntityId = returnerId;
                cov.BreakOnReturner = returnerId > 0;
                beh.State = BehaviorState.MovingToPosition;
                beh.TargetPosition = ClampToField(cov.BreakOnReturner ? ComputeCoverageTarget(world, entityId, cov, targetPos) : cov.LaneLandmark);
                e.Set(cov);
                e.Set(beh);
            });
        }
    }

    private void UpdateReturn(World world, MatchState match, int controlledEntityId)
    {
        foreach (var entityId in GetEntitiesWith<PuntReturn>(world))
        {
            if (entityId == controlledEntityId || !HasBall(world, entityId))
                continue;

            WithEntity(world, entityId, e =>
            {
                var pr = e.Get<PuntReturn>();
                var beh = e.Get<Behavior>();
                var pos = e.Get<Position>().Value;
                var signX = match.OffenseDirection == OffenseDirection.LeftToRight ? -1f : 1f;

                if (pr.LaneLockFrames > 0)
                {
                    pr.LaneLockFrames--;
                }
                else
                {
                    var lookAheadX = pos.X + (36f * signX);
                    var left = new Vector2(lookAheadX, pos.Y - 24f);
                    var center = new Vector2(lookAheadX, pos.Y);
                    var right = new Vector2(lookAheadX, pos.Y + 24f);
                    var sLeft = ScoreLane(world, pos, left);
                    var sCenter = ScoreLane(world, pos, center);
                    var sRight = ScoreLane(world, pos, right);
                    if (sLeft <= sCenter && sLeft <= sRight) { pr.Lane = PuntReturnLane.Left; pr.LastChosenTarget = left; }
                    else if (sCenter <= sRight) { pr.Lane = PuntReturnLane.Center; pr.LastChosenTarget = center; }
                    else { pr.Lane = PuntReturnLane.Right; pr.LastChosenTarget = right; }
                    pr.LaneLockFrames = 20;
                }

                beh.State = BehaviorState.MovingToPosition;
                beh.TargetPosition = ClampToField(pr.LastChosenTarget);
                e.Set(pr);
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
        var signX = match.OffenseDirection == OffenseDirection.LeftToRight ? -1f : 1f;
        var ids = GetPlayersForTeam(world, 1 - match.PossessionTeam);
        ids.Sort();
        var slot = 0;
        foreach (var id in ids)
        {
            if (id == returnerId) continue;
            var laneOffset = (slot % 5) - 2;
            var depth = slot < 4 ? 12f : 22f;
            var target = new Vector2(returnerPos.X + (depth * signX), returnerPos.Y + (laneOffset * 11f));
            slot++;
            WithEntity(world, id, e =>
            {
                var beh = e.Get<Behavior>();
                beh.State = BehaviorState.MovingToPosition;
                beh.TargetPosition = ClampToField(target);
                e.Set(beh);
            });
        }
    }

    private static Vector2 ComputeCoverageTarget(World world, int defenderId, PuntCoverage coverage, Vector2 target)
    {
        if (!coverage.IsGunner)
            return target;
        var myPos = GetPosition(world, defenderId);
        var x = MathHelper.Lerp(myPos.X, target.X, 0.72f);
        var y = MathHelper.Lerp(myPos.Y, target.Y, 0.85f);
        return new Vector2(x, y);
    }

    private static int ScoreLane(World world, Vector2 returnerPos, Vector2 laneTarget)
    {
        var score = 0;
        foreach (var defenderId in GetEntitiesWith<PuntCoverage>(world))
        {
            var p = GetPosition(world, defenderId);
            if (MathF.Abs(p.X - laneTarget.X) > 30f || MathF.Abs(p.Y - laneTarget.Y) > 20f)
                continue;
            score++;
            if (MathF.Abs(p.Y - laneTarget.Y) < 8f)
                score++;
        }
        return score;
    }

    private static void StartPuntFlight(World world, int ballEntityId, Vector2 start, Vector2 end, float durationSeconds, float apexHeight)
    {
        var q = new QueryDescription().WithAll<Ball, Position, Velocity>();
        world.Query(in q, (Entity e, ref Ball ball, ref Position pos, ref Velocity vel) =>
        {
            if (e.Id != ballEntityId)
                return;
            ball.State = BallState.InAir;
            ball.OwnerEntityId = -1;
            ball.FlightKind = BallFlightKind.Punt;
            ball.StartPos = start;
            ball.EndPos = end;
            ball.ElapsedSeconds = 0f;
            ball.DurationSeconds = durationSeconds;
            ball.ApexHeight = apexHeight;
            ball.Height = 0f;
            ball.IsComplete = false;
            pos.Value = start;
            vel.Value = Vector2.Zero;
        });
    }

    private static void EndPlay(PlayState play, WhistleReason reason, int endAbsoluteYard, int yardsGained, bool turnover)
    {
        var whistle = new WhistleEvent(reason.ToString().ToLowerInvariant());
        SimEventBus.Send(ref whistle);
        var ended = new PlayEndedEvent(play.PlayId, (int)reason, endAbsoluteYard, yardsGained, turnover, false, false);
        SimEventBus.Send(ref ended);
    }

    private static int ComputeDefaultLandingAbsoluteYard(MatchState match)
    {
        var lineAbs = PlayState.ToAbsoluteYard(match.BallSpot, match.OffenseDirection);
        var delta = match.OffenseDirection == OffenseDirection.LeftToRight ? 34 : -34;
        return Math.Clamp(lineAbs + delta, 0, 100);
    }

    private static int GetTouchbackAbsoluteYard(int receivingTeam)
        => receivingTeam == 0 ? MatchState.TouchbackSpotYard : 100 - MatchState.TouchbackSpotYard;

    private static bool IsTouchback(OffenseDirection direction, int absoluteYard)
        => direction == OffenseDirection.LeftToRight ? absoluteYard >= 100 : absoluteYard <= 0;

    private static void SetPlayerForSpecialTeams(World world, int entityId, int teamIndex, bool isOffense, Vector2 position)
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

    private static Vector2 ClampToField(Vector2 p)
        => new(MathHelper.Clamp(p.X, FieldLeft, FieldRight), MathHelper.Clamp(p.Y, FieldTop, FieldBottom));

    private void ResetState()
    {
        _preparedPlayId = 0;
        _flightStartedPlayId = 0;
        _resolvedFlightPlayId = 0;
    }

    private delegate void BallMutation(ref Ball ball, ref Position position, ref Velocity velocity);
}
