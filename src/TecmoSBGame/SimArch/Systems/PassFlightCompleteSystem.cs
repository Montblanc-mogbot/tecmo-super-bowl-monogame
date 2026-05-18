using System;
using Arch.Core;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.Events;

namespace TecmoSBGame.SimArch.Systems;

// Ported from: src/TecmoSBGame/ArchiveMge/Systems/PassFlightCompleteSystem.cs

/// <summary>
/// SimArch equivalent of <c>TecmoSBGame.Systems.PassFlightCompleteSystem</c>.
///
/// Minimal deterministic resolution using persisted pass metadata:
/// - Prefer the intended receiver when they are the nearest eligible offense within radius.
/// - Preserve persisted defender/target context so completion logic does not depend on transient QB state.
/// - Resolve explicit catch / incomplete / interception outcomes.
/// </summary>
public sealed class PassFlightCompleteSystem
{
    private const float ELIGIBLE_RADIUS = 14f;
    private const float INTENDED_RECEIVER_RADIUS = 18f;
    private const float DEFENDER_CONTEST_RADIUS = 10f;
    private const float SECURE_CATCH_RADIUS = 6f;
    private const float INTERCEPTION_RADIUS = 8f;
    private const float INTERCEPTION_WIN_MARGIN = 1.5f;
    private const float LEVERAGE_DISTANCE_SCALE = 1.5f;

    public void Update(World world)
    {
        var qBall = new QueryDescription().WithAll<Ball, Position>();
        world.Query(in qBall, (Entity ballEntity, ref Ball ball, ref Position ballPos) =>
        {
            if (ball.FlightKind != BallFlightKind.Pass || !ball.IsComplete)
                return;

            var pos = ballPos.Value;
            var radiusSq = ELIGIBLE_RADIUS * ELIGIBLE_RADIUS;
            var intendedRadiusSq = INTENDED_RECEIVER_RADIUS * INTENDED_RECEIVER_RADIUS;

            var bestId = -1;
            var bestDistSq = float.PositiveInfinity;
            var targetId = ball.TargetEntityId > 0 ? ball.TargetEntityId : -1;
            var targetDistSq = float.PositiveInfinity;

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

                if (e.Id == targetId)
                    targetDistSq = distSq;

                if (distSq < bestDistSq - 0.0001f || (MathF.Abs(distSq - bestDistSq) <= 0.0001f && (bestId < 0 || e.Id < bestId)))
                {
                    bestId = e.Id;
                    bestDistSq = distSq;
                }
            });

            var primaryDefenderId = ResolvePrimaryDefender(world, ball, pos, out var primaryDefenderDistSq);
            var defenderContestSq = DEFENDER_CONTEST_RADIUS * DEFENDER_CONTEST_RADIUS;
            var secureCatchSq = SECURE_CATCH_RADIUS * SECURE_CATCH_RADIUS;
            var interceptionSq = INTERCEPTION_RADIUS * INTERCEPTION_RADIUS;

            var resolvedReceiverId = bestId;
            if (targetId > 0 && targetDistSq <= intendedRadiusSq)
                resolvedReceiverId = targetId;
            else if (targetId > 0 && targetDistSq <= radiusSq && (targetDistSq <= bestDistSq + 0.0001f || bestId < 0))
                resolvedReceiverId = targetId;

            var defenderLeverage = primaryDefenderId > 0 ? GetCoverageLeverage(world, primaryDefenderId, resolvedTargetId: resolvedReceiverId > 0 ? resolvedReceiverId : targetId) : 0f;
            var effectiveDefenderDistSq = AdjustDistanceForLeverage(primaryDefenderDistSq, defenderLeverage);
            var defenderIsContesting = primaryDefenderId > 0 && effectiveDefenderDistSq <= defenderContestSq;

            if (primaryDefenderId > 0 && targetId > 0 && GetCoverageLeverage(world, primaryDefenderId, resolvedTargetId: targetId) >= 8f && targetDistSq >= secureCatchSq)
                resolvedReceiverId = -1;

            var winningReceiverDistSq = resolvedReceiverId == targetId ? targetDistSq : bestDistSq;
            var isSecureIntendedCatch = resolvedReceiverId == targetId && targetDistSq <= secureCatchSq;
            var defenderHasInsideLeverage = primaryDefenderId > 0 && targetId > 0 && IsDefenderInsideLeverage(world, primaryDefenderId, targetId, pos);
            var heavyBallHawk = defenderLeverage >= 4f;
            var extremeBallHawk = defenderLeverage >= 8f;
            var receiverWinsContest = resolvedReceiverId >= 0 && (
                !defenderIsContesting
                || winningReceiverDistSq + 4f <= effectiveDefenderDistSq
                || (isSecureIntendedCatch && !heavyBallHawk && !defenderHasInsideLeverage));
            var defenderTargetAdvantageSq = resolvedReceiverId == targetId && targetId > 0
                ? MathF.Max(0f, targetDistSq - effectiveDefenderDistSq)
                : 0f;
            var defenderWinsInterception = primaryDefenderId > 0
                && effectiveDefenderDistSq <= interceptionSq
                && (resolvedReceiverId < 0
                    || effectiveDefenderDistSq + INTERCEPTION_WIN_MARGIN < winningReceiverDistSq
                    || (extremeBallHawk && defenderTargetAdvantageSq >= 1f)
                    || (heavyBallHawk && defenderHasInsideLeverage))
                && !(isSecureIntendedCatch && !heavyBallHawk && !defenderHasInsideLeverage);

            if (receiverWinsContest)
            {
                ball.State = TecmoSBGame.SimArch.Components.BallState.Held;
                ball.OwnerEntityId = resolvedReceiverId;
                ball.FlightKind = BallFlightKind.None;
                ball.Height = 0f;
                ball.IsComplete = true;

                var caught = new BallCaughtEvent(resolvedReceiverId, pos);
                SimEventBus.Send(ref caught);

                var resolved = new PassResolvedEvent(
                    PassOutcome.Catch,
                    ball.PasserEntityId,
                    ball.TargetEntityId > 0 ? ball.TargetEntityId : null,
                    ball.IntendedReceiverRoleId,
                    ball.IntendedReceiverSlot,
                    resolvedReceiverId,
                    primaryDefenderId > 0 ? primaryDefenderId : null,
                    ball.PassTargetPosition,
                    pos);
                SimEventBus.Send(ref resolved);
            }
            else if (defenderWinsInterception)
            {
                ball.State = TecmoSBGame.SimArch.Components.BallState.Held;
                ball.OwnerEntityId = primaryDefenderId;
                ball.FlightKind = BallFlightKind.None;
                ball.Height = 0f;
                ball.IsComplete = true;

                UpdateTeamPossession(world, primaryDefenderId);

                var resolved = new PassResolvedEvent(
                    PassOutcome.Interception,
                    ball.PasserEntityId,
                    ball.TargetEntityId > 0 ? ball.TargetEntityId : null,
                    ball.IntendedReceiverRoleId,
                    ball.IntendedReceiverSlot,
                    primaryDefenderId,
                    primaryDefenderId,
                    ball.PassTargetPosition,
                    pos);
                SimEventBus.Send(ref resolved);
            }
            else
            {
                ball.State = TecmoSBGame.SimArch.Components.BallState.Dead;
                ball.OwnerEntityId = -1;
                ball.FlightKind = BallFlightKind.None;
                ball.Height = 0f;
                ball.IsComplete = true;

                var whistle = new WhistleEvent("incomplete");
                SimEventBus.Send(ref whistle);

                var resolved = new PassResolvedEvent(
                    PassOutcome.Incomplete,
                    ball.PasserEntityId,
                    ball.TargetEntityId > 0 ? ball.TargetEntityId : null,
                    ball.IntendedReceiverRoleId,
                    ball.IntendedReceiverSlot,
                    null,
                    primaryDefenderId > 0 ? primaryDefenderId : null,
                    ball.PassTargetPosition,
                    pos);
                SimEventBus.Send(ref resolved);
            }
        });
    }

    private static int ResolvePrimaryDefender(World world, in Ball ball, Vector2 targetPos, out float defenderDistSq)
    {
        var localDistSq = float.PositiveInfinity;

        if (ball.NearestDefenderEntityId > 0 && TryGetPosition(world, ball.NearestDefenderEntityId, out var persistedPos))
        {
            defenderDistSq = Vector2.DistanceSquared(persistedPos, targetPos);
            return ball.NearestDefenderEntityId;
        }

        var nearestId = -1;
        var q = new QueryDescription().WithAll<Position, Team>();
        world.Query(in q, (Entity e, ref Position p, ref Team t) =>
        {
            if (t.IsOffense)
                return;

            var distSq = Vector2.DistanceSquared(p.Value, targetPos);
            if (distSq < localDistSq - 0.0001f || (MathF.Abs(distSq - localDistSq) <= 0.0001f && (nearestId < 0 || e.Id < nearestId)))
            {
                nearestId = e.Id;
                localDistSq = distSq;
            }
        });

        defenderDistSq = localDistSq;
        return nearestId;
    }

    private static float AdjustDistanceForLeverage(float distanceSq, float leverage)
    {
        if (float.IsPositiveInfinity(distanceSq))
            return distanceSq;

        return MathF.Max(0f, distanceSq - (leverage * LEVERAGE_DISTANCE_SCALE));
    }

    private static float GetCoverageLeverage(World world, int defenderId, int resolvedTargetId)
    {
        var leverage = 0f;
        var found = false;

        var q = new QueryDescription().WithAll<Coverage>();
        world.Query(in q, (Entity e, ref Coverage coverage) =>
        {
            if (found || e.Id != defenderId)
                return;

            leverage = coverage.BallHawkLeverage;
            if (resolvedTargetId > 0 && coverage.AssignmentTargetId == resolvedTargetId)
                leverage += 1f;
            if (coverage.BreakDelayFrames <= 0)
                leverage += 0.5f;
            found = true;
        });

        return leverage;
    }

    private static bool IsDefenderInsideLeverage(World world, int defenderId, int receiverId, Vector2 catchPoint)
    {
        if (!TryGetPosition(world, defenderId, out var defenderPos) || !TryGetPosition(world, receiverId, out var receiverPos))
            return false;

        var defenderToCatch = Vector2.DistanceSquared(defenderPos, catchPoint);
        var receiverToCatch = Vector2.DistanceSquared(receiverPos, catchPoint);
        if (defenderToCatch > receiverToCatch + 6f)
            return false;

        var centerY = catchPoint.Y;
        var defenderInside = MathF.Abs(defenderPos.Y - centerY) <= MathF.Abs(receiverPos.Y - centerY) + 1f;
        var defenderAhead = defenderPos.X <= receiverPos.X + 1f;
        return defenderInside && defenderAhead;
    }

    private static bool TryGetPosition(World world, int entityId, out Vector2 pos)
    {
        var localPos = default(Vector2);
        var found = false;

        var q = new QueryDescription().WithAll<Position>();
        world.Query(in q, (Entity e, ref Position p) =>
        {
            if (found || e.Id != entityId)
                return;

            localPos = p.Value;
            found = true;
        });

        pos = localPos;
        return found;
    }

    private static void UpdateTeamPossession(World world, int newOwnerEntityId)
    {
        var newOwnerTeamIndex = -1;
        var qFind = new QueryDescription().WithAll<Team>();
        world.Query(in qFind, (Entity e, ref Team team) =>
        {
            if (newOwnerTeamIndex >= 0 || e.Id != newOwnerEntityId)
                return;

            newOwnerTeamIndex = team.TeamIndex;
        });

        if (newOwnerTeamIndex < 0)
            return;

        var qTeams = new QueryDescription().WithAll<Team>();
        world.Query(in qTeams, (Entity _, ref Team team) =>
        {
            team.IsOffense = team.TeamIndex == newOwnerTeamIndex;
        });
    }
}
