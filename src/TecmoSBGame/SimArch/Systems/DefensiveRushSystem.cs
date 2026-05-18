using System;
using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

// Ported from: src/TecmoSBGame/ArchiveMge/Systems/RushSystem.cs

/// <summary>
/// Defensive rush (SimArch scaffold).
///
/// Pattern:
/// - Each defender with <see cref="Rush"/> computes a gap/contain landmark at play start.
/// - Move toward landmark until reached.
/// - Then transition to a QB rush target that preserves contain leverage when applicable.
/// - Nearby free rushers raise deterministic QB pressure.
/// </summary>
public sealed class DefensiveRushSystem
{
    public float LandmarkReachRadius = 6f;
    public float PressureRadius = 28f;
    public float TightPressureRadius = 16f;
    public float PocketHoldRadius = 14f;

    public void Update(World world, float dtSeconds, IReadOnlyList<int> defenseEntityIds)
    {
        if (dtSeconds <= 0f)
            return;

        var qbId = FindQb(world);
        if (qbId < 0)
            return;

        if (!TryGetPosition(world, qbId, out var qbPos))
            return;

        ResetPressure(world, qbId);

        var q = new QueryDescription().WithAll<Rush, Position, Behavior, Team>();

        var allow = new HashSet<int>(defenseEntityIds);
        var reachSq = LandmarkReachRadius * LandmarkReachRadius;
        var pressureSq = PressureRadius * PressureRadius;
        var tightPressureSq = TightPressureRadius * TightPressureRadius;
        var pressureFrames = Math.Max(1, (int)MathF.Round(dtSeconds * 60f));

        world.Query(in q, (Entity e, ref Rush rush, ref Position pos, ref Behavior beh, ref Team team) =>
        {
            if (!allow.Contains(e.Id) || team.IsOffense)
                return;

            if (!rush.HasLandmark)
            {
                rush.Landmark = ComputeLandmark(qbPos, rush);
                rush.HasLandmark = true;
                rush.ReachedLandmark = false;
            }

            if (!rush.ReachedLandmark)
            {
                beh.State = BehaviorState.MovingToPosition;
                beh.TargetEntityId = -1;
                beh.TargetPosition = rush.Landmark;

                if (Vector2.DistanceSquared(pos.Value, rush.Landmark) <= reachSq)
                    rush.ReachedLandmark = true;

                return;
            }

            if (rush.Engaged && beh.State != BehaviorState.Engaged)
            {
                var brokeFree = rush.EngagementFrames >= 12;
                rush.Engaged = false;
                rush.EngagedBlockerId = -1;
                rush.EngagementFrames = 0;
                if (brokeFree)
                    rush.FailedRushMoveCount = Math.Max(0, rush.FailedRushMoveCount - 1);
            }

            if (rush.Engaged)
            {
                rush.EngagementFrames += pressureFrames;
                AttemptRushDisengage(world, e.Id, ref rush, pressureFrames);
                if (rush.Engaged)
                    return;
            }

            var rushTarget = ComputeRushTarget(qbPos, pos.Value, rush);
            var hasPocketSeal = HasPocketSeal(world, e.Id, pos.Value, qbPos, PocketHoldRadius);
            beh.State = BehaviorState.MovingToPosition;
            beh.TargetEntityId = qbId;
            beh.TargetPosition = hasPocketSeal ? HoldContainTarget(qbPos, pos.Value, rushTarget) : rushTarget;

            var distSq = Vector2.DistanceSquared(pos.Value, qbPos);
            if (distSq > pressureSq)
                return;

            var protectionPenalty = GetProtectionPenalty(world, e.Id, pos.Value, qbPos);
            var rushMultiplier = rush.IsContain ? 1 : 2;
            var moveMomentumBonus = Math.Min(3, Math.Max(0, 2 - rush.FailedRushMoveCount));
            var addedFrames = distSq <= tightPressureSq
                ? pressureFrames * rushMultiplier
                : Math.Max(1, pressureFrames * (rushMultiplier - 1));
            addedFrames = Math.Max(0, addedFrames + moveMomentumBonus - protectionPenalty);
            if (addedFrames > 0)
                SetPressure(world, qbId, addedFrames);
        });
    }

    private static void AttemptRushDisengage(World world, int defenderId, ref Rush rush, int frameAdvance)
    {
        if (rush.EngagedBlockerId < 0)
            return;

        rush.LastRushMoveFrame += frameAdvance;
        if (rush.LastRushMoveFrame < Rush.RUSH_MOVE_COOLDOWN)
            return;

        rush.LastRushMoveFrame = 0;

        var rusherStrength = GetRushStrength(world, defenderId);
        var blockerStrength = GetBlockStrength(world, rush.EngagedBlockerId);
        var supportPenalty = CountSupporters(world, defenderId) * 12f;
        var moveScore = rush.Type switch
        {
            RushType.Power or RushType.Bull => rusherStrength - blockerStrength,
            RushType.Swim or RushType.Spin => (rusherStrength * 0.85f) - (blockerStrength * 0.65f),
            _ => rusherStrength - blockerStrength,
        };

        moveScore += MathF.Min(18f, rush.EngagementFrames * 0.35f);
        moveScore -= supportPenalty;

        if (moveScore >= 6f)
        {
            rush.Engaged = false;
            rush.EngagedBlockerId = -1;
            rush.EngagementFrames = 0;
            rush.FailedRushMoveCount = 0;
        }
        else
        {
            rush.FailedRushMoveCount = Math.Min(6, rush.FailedRushMoveCount + 1);
        }
    }

    private static int CountSupporters(World world, int defenderId)
    {
        var support = 0;
        var q = new QueryDescription().WithAll<BlockTarget, Team>();
        world.Query(in q, (Entity _, ref BlockTarget blockTarget, ref Team team) =>
        {
            if (!team.IsOffense)
                return;

            if (blockTarget.TargetEntityId == defenderId || blockTarget.EngagedEntityId == defenderId)
                support++;
        });

        return Math.Max(0, support - 1);
    }

    private static float GetBlockStrength(World world, int entityId)
    {
        var strength = 50f;
        var q = new QueryDescription().WithAll<Ratings>();
        world.Query(in q, (Entity e, ref Ratings ratings) =>
        {
            if (e.Id != entityId)
                return;

            strength = (ratings.HP * 0.65f) + (ratings.RS * 0.35f);
        });

        return strength;
    }

    private static float GetRushStrength(World world, int entityId)
    {
        var strength = 50f;
        var q = new QueryDescription().WithAll<Ratings>();
        world.Query(in q, (Entity e, ref Ratings ratings) =>
        {
            if (e.Id != entityId)
                return;

            strength = (ratings.HP * 0.55f) + (ratings.MS * 0.45f);
        });

        return strength;
    }

    private static Vector2 ComputeLandmark(Vector2 qbPos, in Rush rush)
    {
        var basePos = qbPos + new Vector2(0, 14);

        if (rush.TargetGap != default || rush.IsContain)
            return ComputeGapLandmark(qbPos, rush.TargetGap, rush.IsContain);

        var dx = rush.Assignment switch
        {
            RushAssignment.AGapLeft => -6,
            RushAssignment.AGapRight => 6,
            RushAssignment.BGapLeft => -14,
            RushAssignment.BGapRight => 14,
            RushAssignment.EdgeLeft => -26,
            RushAssignment.EdgeRight => 26,
            _ => 0,
        };

        return basePos + new Vector2(dx, 0);
    }

    private static Vector2 ComputeGapLandmark(Vector2 qbPos, RushGap gap, bool contain)
    {
        if (contain)
        {
            return gap == RushGap.ContainLeft
                ? qbPos + new Vector2(-22f, -18f)
                : qbPos + new Vector2(-22f, 18f);
        }

        return gap switch
        {
            RushGap.ALeft => qbPos + new Vector2(-10f, -4f),
            RushGap.ARight => qbPos + new Vector2(-10f, 4f),
            RushGap.BLeft => qbPos + new Vector2(-12f, -12f),
            RushGap.BRight => qbPos + new Vector2(-12f, 12f),
            RushGap.CLeft => qbPos + new Vector2(-14f, -20f),
            RushGap.CRight => qbPos + new Vector2(-14f, 20f),
            RushGap.ContainLeft => qbPos + new Vector2(-22f, -18f),
            RushGap.ContainRight => qbPos + new Vector2(-22f, 18f),
            _ => qbPos + new Vector2(-10f, 0f),
        };
    }

    private static Vector2 ComputeRushTarget(Vector2 qbPos, Vector2 rusherPos, in Rush rush)
    {
        if (!rush.IsContain)
            return qbPos + new Vector2(-6f, 0f);

        var target = qbPos + new Vector2(-4f, 0f);
        var side = rusherPos.Y <= qbPos.Y ? -1f : 1f;
        target.Y = qbPos.Y + (side * 20f);
        return target;
    }

    private static Vector2 HoldContainTarget(Vector2 qbPos, Vector2 rusherPos, Vector2 rushTarget)
    {
        var side = rusherPos.Y <= qbPos.Y ? -1f : 1f;
        return new Vector2(MathF.Min(rushTarget.X, qbPos.X - 10f), qbPos.Y + (side * 16f));
    }

    private static int GetProtectionPenalty(World world, int defenderId, Vector2 defenderPos, Vector2 qbPos)
    {
        var penalty = 0;
        var q = new QueryDescription().WithAll<BlockTarget, Position, Team>();
        world.Query(in q, (Entity _, ref BlockTarget blockTarget, ref Position position, ref Team team) =>
        {
            if (!team.IsOffense)
                return;

            if (blockTarget.EngagedEntityId != defenderId && blockTarget.TargetEntityId != defenderId)
                return;

            var distSq = Vector2.DistanceSquared(position.Value, defenderPos);
            if (distSq <= 36f)
                penalty += blockTarget.IsDoubleTeam ? 3 : 2;
            else if (position.Value.X <= qbPos.X && position.Value.X >= defenderPos.X - 6f)
                penalty += 1;
        });

        return penalty;
    }

    private static bool HasPocketSeal(World world, int defenderId, Vector2 defenderPos, Vector2 qbPos, float holdRadius)
    {
        var holdSq = holdRadius * holdRadius;
        var sealedPocket = false;
        var q = new QueryDescription().WithAll<BlockTarget, Position, Team>();
        world.Query(in q, (Entity e, ref BlockTarget blockTarget, ref Position position, ref Team team) =>
        {
            if (sealedPocket || !team.IsOffense)
                return;

            if (blockTarget.EngagedEntityId != defenderId && blockTarget.TargetEntityId != defenderId)
                return;

            if (Vector2.DistanceSquared(position.Value, defenderPos) > holdSq)
                return;

            var betweenQbAndDefender = position.Value.X <= qbPos.X && position.Value.X >= defenderPos.X - 6f;
            if (betweenQbAndDefender)
                sealedPocket = true;
        });

        return sealedPocket;
    }

    private static void ResetPressure(World world, int qbId)
    {
        var q = new QueryDescription().WithAll<QbBrain>();
        world.Query(in q, (Entity e, ref QbBrain qb) =>
        {
            if (e.Id != qbId)
                return;

            qb.PressureDetected = false;
            qb.PressureFrameCount = Math.Max(0, qb.PressureFrameCount - 2);
        });
    }

    private static void SetPressure(World world, int qbId, int addedFrames)
    {
        var q = new QueryDescription().WithAll<QbBrain>();
        world.Query(in q, (Entity e, ref QbBrain qb) =>
        {
            if (e.Id != qbId)
                return;

            qb.PressureDetected = true;
            qb.PressureFrameCount += Math.Max(1, addedFrames);
        });
    }

    private static int FindQb(World world)
    {
        var qb = -1;
        var q = new QueryDescription().WithAll<Role, Team>();
        world.Query(in q, (Entity e, ref Role r, ref Team t) =>
        {
            if (qb != -1)
                return;
            if (!t.IsOffense)
                return;
            if (r.Id != RoleId.QB)
                return;

            qb = e.Id;
        });

        return qb;
    }

    private static bool TryGetPosition(World world, int entityId, out Vector2 pos)
    {
        pos = default;
        var found = false;
        var local = Vector2.Zero;

        var q = new QueryDescription().WithAll<Position>();
        world.Query(in q, (Entity e, ref Position p) =>
        {
            if (found)
                return;
            if (e.Id != entityId)
                return;

            local = p.Value;
            found = true;
        });

        if (!found)
            return false;

        pos = local;
        return true;
    }
}
