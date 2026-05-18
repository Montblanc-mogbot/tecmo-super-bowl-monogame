using System;
using System.Collections.Generic;
using System.Linq;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

// Ported from: src/TecmoSBGame/ArchiveMge/Systems/QbDropbackSystem.cs
// Ported from: src/TecmoSBGame/ArchiveMge/Systems/ReadProgressionSystem.cs

/// <summary>
/// QB dropback + read progression + pass decision (SimArch).
///
/// Current scope:
/// - Only runs when ball is held by the QB.
/// - Drop back for a fixed number of frames.
/// - Then start a pass flight toward the best currently viable read.
///
/// This is intentionally bounded; fuller ROM-accurate QB logic comes later.
/// </summary>
public sealed class QbAiSystem
{
    // Default read order (slot names mirrored by RoleId).
    private static readonly RoleId[] ReadOrder =
    [
        RoleId.WR1,
        RoleId.WR2,
        RoleId.TE,
        RoleId.HB,
        RoleId.FB,
    ];

    public void Update(World world, float dtSeconds, int ballEntityId)
    {
        if (dtSeconds <= 0f)
            return;

        var (ballState, ownerId) = GetBallOwner(world, ballEntityId);
        if (ballState != BallState.Held || ownerId < 0)
            return;

        if (!TryGetRole(world, ownerId, out var role) || role.Id != RoleId.QB)
            return;

        var q = new QueryDescription().WithAll<QbBrain>();
        world.Query(in q, (Entity e, ref QbBrain qb) =>
        {
            if (e.Id != ownerId || qb.PassRequested)
                return;

            var frames = Math.Max(0, (int)MathF.Round(dtSeconds * 60f));
            var acceleratedDropback = qb.PressureDetected && qb.DropbackFramesRemaining > 0;
            qb.DropbackFramesRemaining = Math.Max(0, qb.DropbackFramesRemaining - frames);
            if (acceleratedDropback)
                qb.DropbackFramesRemaining = Math.Max(0, qb.DropbackFramesRemaining - frames);

            qb.ReadTimer += qb.PressureFrameCount >= Math.Max(1, qb.PressureThresholdFrames / 2)
                ? frames * 2
                : frames;

            if (qb.DropbackFramesRemaining > 0)
            {
                ApplyPocketSlide(world, ownerId, 12f);
                return;
            }

            var reads = BuildReadCandidates(world, qb, ownerId);
            if (reads.Count == 0)
                return;

            var selectedReadIndex = Math.Clamp(qb.ReadIndex, 0, reads.Count - 1);

            if (qb.PressureFrameCount >= qb.PressureThresholdFrames && selectedReadIndex < reads.Count - 1)
            {
                qb.ReadIndex = selectedReadIndex + 1;
                qb.ReadTimer = 0;
                selectedReadIndex = qb.ReadIndex;
            }
            else if (qb.ReadTimer >= qb.ReadTimeLimitFrames && selectedReadIndex < reads.Count - 1)
            {
                qb.ReadIndex = selectedReadIndex + 1;
                qb.ReadTimer = 0;
                selectedReadIndex = qb.ReadIndex;
            }

            var selected = reads[selectedReadIndex];
            if (!selected.IsThrowWindowOpen)
            {
                if (selectedReadIndex < reads.Count - 1)
                {
                    qb.ReadIndex = selectedReadIndex + 1;
                    qb.ReadTimer = 0;
                    return;
                }

                ApplyPocketSlide(world, ownerId, selected.CoveragePenalty >= 4f ? 10f : 6f);
                return;
            }

            qb.TargetReceiverId = selected.EntityId;
            qb.ThrowTarget = selected.Position;
            PassFlightStartSystem.StartPass(world, ballEntityId, passerEntityId: ownerId, targetEntityId: selected.EntityId, qb.PassType);
            qb.PassRequested = true;

            Console.WriteLine($"[sim-arch] qb pass requested target={selected.EntityId} readIndex={selectedReadIndex} pressure={qb.PressureFrameCount} coverage={selected.CoveragePenalty:0.00} type={qb.PassType}");
        });
    }

    private static List<ReadCandidate> BuildReadCandidates(World world, QbBrain qb, int qbId)
    {
        var offense = new List<ReadCandidate>();
        var defenderPositions = GetDefenderPositions(world);
        var qbPos = GetPosition(world, qbId);
        var coverageByTarget = GetCoverageByTarget(world);

        var q = new QueryDescription().WithAll<Role, Team, Position>();
        world.Query(in q, (Entity e, ref Role r, ref Team t, ref Position p) =>
        {
            if (!t.IsOffense || e.Id == qbId)
                return;
            if (!IsEligibleReadRole(r.Id))
                return;

            var baseOrder = GetBaseReadOrderIndex(r.Id);
            var nearestDefenderDist = GetNearestDefenderDistance(defenderPositions, p.Value);
            var separation = Math.Max(0f, nearestDefenderDist - 4f);
            var coveragePenalty = coverageByTarget.TryGetValue(e.Id, out var targetCoverage)
                ? targetCoverage
                : 0f;
            coveragePenalty += Math.Max(0f, 10f - separation) * 0.35f;

            var distanceFromQb = Vector2.Distance(qbPos, p.Value);
            var onBreak = IsNearBehaviorTarget(world, e.Id, p.Value, radius: 6f);
            var throwWindow = onBreak || separation >= 5f || (qb.PressureDetected && separation >= 3f);
            if (coveragePenalty >= 6f && separation <= 4f)
                throwWindow = false;

            var score = baseOrder * 10f + coveragePenalty - separation;
            score += Math.Max(0f, distanceFromQb - 12f) * 0.12f;
            if (qb.PressureDetected)
            {
                score -= distanceFromQb * 0.18f;
                score += coveragePenalty * 0.4f;
            }

            offense.Add(new ReadCandidate(e.Id, r.Id, p.Value, score, coveragePenalty, throwWindow));
        });

        return offense
            .OrderBy(c => c.Score)
            .ThenBy(c => c.EntityId)
            .ToList();
    }

    private static void ApplyPocketSlide(World world, int qbId, float lateralOffset)
    {
        var q = new QueryDescription().WithAll<Behavior, Position>();
        world.Query(in q, (Entity e, ref Behavior b, ref Position p) =>
        {
            if (e.Id != qbId)
                return;

            b.State = BehaviorState.MovingToPosition;
            b.TargetEntityId = -1;
            b.TargetPosition = p.Value + new Vector2(-lateralOffset, -20);
        });
    }

    private static bool IsEligibleReadRole(RoleId role)
        => role is RoleId.WR1 or RoleId.WR2 or RoleId.TE or RoleId.HB or RoleId.FB;

    private static int GetBaseReadOrderIndex(RoleId role)
    {
        for (var i = 0; i < ReadOrder.Length; i++)
        {
            if (ReadOrder[i] == role)
                return i;
        }

        return ReadOrder.Length;
    }

    private static Dictionary<int, float> GetCoverageByTarget(World world)
    {
        var result = new Dictionary<int, float>();
        var q = new QueryDescription().WithAll<Coverage, Team>();
        world.Query(in q, (Entity _, ref Coverage coverage, ref Team team) =>
        {
            if (team.IsOffense || coverage.AssignmentTargetId <= 0)
                return;

            var penalty = 2f + coverage.BallHawkLeverage + (coverage.BreakDelayFrames <= 1 ? 1f : 0f);
            if (result.TryGetValue(coverage.AssignmentTargetId, out var existing))
                result[coverage.AssignmentTargetId] = Math.Max(existing, penalty);
            else
                result[coverage.AssignmentTargetId] = penalty;
        });
        return result;
    }

    private static List<Vector2> GetDefenderPositions(World world)
    {
        var result = new List<Vector2>();
        var q = new QueryDescription().WithAll<Position, Team>();
        world.Query(in q, (Entity _, ref Position position, ref Team team) =>
        {
            if (!team.IsOffense)
                result.Add(position.Value);
        });
        return result;
    }

    private static float GetNearestDefenderDistance(List<Vector2> defenders, Vector2 target)
    {
        var best = float.PositiveInfinity;
        foreach (var defender in defenders)
        {
            var dist = Vector2.Distance(defender, target);
            if (dist < best)
                best = dist;
        }

        return float.IsPositiveInfinity(best) ? 999f : best;
    }

    private static bool IsNearBehaviorTarget(World world, int entityId, Vector2 position, float radius)
    {
        var radiusSq = radius * radius;
        var near = false;
        var q = new QueryDescription().WithAll<Behavior>();
        world.Query(in q, (Entity e, ref Behavior behavior) =>
        {
            if (near || e.Id != entityId)
                return;

            near = Vector2.DistanceSquared(behavior.TargetPosition, position) <= radiusSq;
        });

        return near;
    }

    private static Vector2 GetPosition(World world, int entityId)
    {
        var pos = Vector2.Zero;
        var found = false;
        var q = new QueryDescription().WithAll<Position>();
        world.Query(in q, (Entity e, ref Position p) =>
        {
            if (found || e.Id != entityId)
                return;
            pos = p.Value;
            found = true;
        });
        return pos;
    }

    private static (BallState State, int OwnerId) GetBallOwner(World world, int ballEntityId)
    {
        var state = BallState.Dead;
        var owner = -1;
        var found = false;

        var q = new QueryDescription().WithAll<Ball>();
        world.Query(in q, (Entity e, ref Ball b) =>
        {
            if (found)
                return;
            if (e.Id != ballEntityId)
                return;

            state = b.State;
            owner = b.OwnerEntityId;
            found = true;
        });

        return (state, owner);
    }

    private static bool TryGetRole(World world, int entityId, out Role role)
    {
        role = default;
        var found = false;
        var local = default(Role);

        var q = new QueryDescription().WithAll<Role>();
        world.Query(in q, (Entity e, ref Role r) =>
        {
            if (found)
                return;
            if (e.Id != entityId)
                return;
            local = r;
            found = true;
        });

        if (!found)
            return false;

        role = local;
        return true;
    }

    private readonly record struct ReadCandidate(
        int EntityId,
        RoleId RoleId,
        Vector2 Position,
        float Score,
        float CoveragePenalty,
        bool IsThrowWindowOpen);
}
