using System;
using System.Collections.Generic;
using System.Linq;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

// Ported from: src/TecmoSBGame/ArchiveMge/Systems/BlockerAISystem.cs

/// <summary>
/// Assignment-based blocking AI (SimArch scaffold).
///
/// Current scope:
/// - Respect authored block intent where formation/play data exposes it.
/// - Keep route runners out of the trench logic.
/// - Drive blockers toward defender targets with deterministic occupancy penalties.
/// - Support conservative second-level climbs and double-team marking.
/// </summary>
public sealed class BlockerAiSystem
{
    private const int RELEASE_TO_SECOND_LEVEL_AFTER_FRAMES = 28;
    private const int REASSIGN_STICK_FRAMES = 14;
    private const int PRESSURE_WIN_FRAMES = 20;
    private const int FAILED_ENGAGEMENT_REASSIGN_FRAMES = 10;
    private const float DOUBLE_TEAM_DEFENDER_SPEED_MULT = 0.45f;
    private const float DOUBLE_TEAM_REFRESH_SECONDS = 0.18f;
    private const float PASS_PROTECTION_HELP_RADIUS = 22f;
    private const float SECOND_LEVEL_CLIMB_DISTANCE = 18f;

    public void Update(World world, float dtSeconds, IReadOnlyList<int> offenseEntityIds, IReadOnlyList<int> defenseEntityIds, int ballEntityId)
    {
        var carrierId = FindBallOwner(world, ballEntityId);
        var defenders = new HashSet<int>(defenseEntityIds);
        if (defenders.Count == 0)
            return;

        var routeRunners = GatherActiveRouteRunners(world, offenseEntityIds);
        var defenderProfiles = GatherDefenderProfiles(world, defenseEntityIds);
        var reservedTargets = new Dictionary<int, int>();

        ClearDoubleTeamFlags(world, offenseEntityIds);

        var frames = Math.Max(1, (int)MathF.Round(dtSeconds * 60f));

        foreach (var blockerId in offenseEntityIds.OrderBy(i => i))
        {
            if (blockerId == carrierId || routeRunners.Contains(blockerId))
                continue;

            if (!TryGet(world, blockerId, out var bt, out var beh, out var pos, out var role, out var playerRole))
                continue;

            SyncEngagementFlags(ref bt, in beh, frames);

            if (bt.IsEngaged)
            {
                bt.AssignmentStickFrames = REASSIGN_STICK_FRAMES;
                bt.PressureContributionFrames = 0;
                bt.FailedEngagements = 0;
                reservedTargets[bt.EngagedEntityId] = reservedTargets.TryGetValue(bt.EngagedEntityId, out var engagedCount)
                    ? engagedCount + 1
                    : 1;
                Store(world, blockerId, bt, beh);
                continue;
            }

            if (bt.AssignmentStickFrames > 0)
                bt.AssignmentStickFrames = Math.Max(0, bt.AssignmentStickFrames - frames);

            if (bt.TargetEntityId >= 0 && TryGetPosition(world, bt.TargetEntityId, out var priorTargetPos))
            {
                var targetDriftSq = Vector2.DistanceSquared(pos.Value, priorTargetPos);
                if (bt.LastTargetDistanceSq + 6f < targetDriftSq)
                    bt.FailedEngagements = Math.Min(6, bt.FailedEngagements + 1);
                else if (targetDriftSq + 3f < bt.LastTargetDistanceSq)
                    bt.FailedEngagements = Math.Max(0, bt.FailedEngagements - 1);
            }

            if (ShouldClimbToSecondLevel(bt, pos.Value, defenderProfiles))
            {
                bt.Assignment = BlockAssignmentType.SecondLevel;
                bt.TargetEntityId = -1;
                bt.AssignmentStickFrames = 0;
            }

            var targetStillValid = bt.TargetEntityId != -1 && defenders.Contains(bt.TargetEntityId);
            var shouldReassign = !targetStillValid || bt.AssignmentStickFrames <= 0 || bt.FailedEngagements >= 2;
            if (targetStillValid && TryGetDefenderProfile(bt.TargetEntityId, defenderProfiles, out var currentTarget))
            {
                var currentDistSq = Vector2.DistanceSquared(pos.Value, currentTarget.Position);
                bt.LastTargetDistanceSq = currentDistSq;
                var losingToPressure = currentTarget.HasRush && currentDistSq > PASS_PROTECTION_HELP_RADIUS * PASS_PROTECTION_HELP_RADIUS;
                if (losingToPressure)
                    bt.PressureContributionFrames += frames;
                else
                    bt.PressureContributionFrames = Math.Max(0, bt.PressureContributionFrames - frames);

                shouldReassign |= bt.PressureContributionFrames >= PRESSURE_WIN_FRAMES;
            }
            else
            {
                bt.PressureContributionFrames = 0;
                bt.LastTargetDistanceSq = float.PositiveInfinity;
            }

            if (shouldReassign)
            {
                bt.TargetEntityId = ChooseTargetDefender(
                    pos.Value,
                    role.Id,
                    playerRole.Slot,
                    bt,
                    defenderProfiles,
                    reservedTargets,
                    carrierId);
                bt.AssignmentStickFrames = bt.TargetEntityId >= 0
                    ? (bt.FailedEngagements > 0 ? FAILED_ENGAGEMENT_REASSIGN_FRAMES : REASSIGN_STICK_FRAMES)
                    : 0;
                bt.PressureContributionFrames = 0;
            }

            if (bt.TargetEntityId >= 0)
                reservedTargets[bt.TargetEntityId] = reservedTargets.TryGetValue(bt.TargetEntityId, out var existing) ? existing + 1 : 1;

            if (bt.TargetEntityId == -1)
            {
                beh.State = BehaviorState.Idle;
                Store(world, blockerId, bt, beh);
                continue;
            }

            if (TryGetPosition(world, bt.TargetEntityId, out var targetPos))
            {
                bt.LastTargetDistanceSq = Vector2.DistanceSquared(pos.Value, targetPos);
                beh.State = BehaviorState.MovingToPosition;
                beh.TargetEntityId = bt.TargetEntityId;
                beh.TargetPosition = targetPos + ComputeApproachOffset(role.Id, bt, pos.Value, targetPos);
            }

            Store(world, blockerId, bt, beh);
        }

        ApplyDoubleTeams(world, offenseEntityIds);
    }

    private static bool ShouldClimbToSecondLevel(in BlockTarget bt, Vector2 blockerPos, IReadOnlyList<DefenderProfile> defenders)
    {
        if (bt.Assignment == BlockAssignmentType.SecondLevel)
            return false;

        if (bt.EngagementFrame < RELEASE_TO_SECOND_LEVEL_AFTER_FRAMES)
            return false;

        foreach (var defender in defenders)
        {
            if (defender.RoleKind != PlayerRoleKind.LB && defender.RoleKind != PlayerRoleKind.DB)
                continue;

            var dx = defender.Position.X - blockerPos.X;
            if (dx < -4f || dx > SECOND_LEVEL_CLIMB_DISTANCE)
                continue;

            return true;
        }

        return false;
    }

    private static void SyncEngagementFlags(ref BlockTarget bt, in Behavior beh, int frames)
    {
        if (beh.State == BehaviorState.Engaged)
        {
            if (!bt.IsEngaged)
            {
                bt.IsEngaged = true;
                bt.EngagedEntityId = beh.TargetEntityId;
                bt.EngagementFrame = 0;
            }
            else
            {
                bt.EngagementFrame += frames;
                bt.EngagedEntityId = beh.TargetEntityId;
            }

            return;
        }

        if (bt.IsEngaged)
        {
            bt.IsEngaged = false;
            bt.EngagedEntityId = -1;
            bt.IsDoubleTeam = false;
        }
    }

    private static int ChooseTargetDefender(
        Vector2 blockerPos,
        RoleId roleId,
        string blockerSlot,
        BlockTarget blockTarget,
        IReadOnlyList<DefenderProfile> defenders,
        IReadOnlyDictionary<int, int> reservedTargets,
        int carrierId)
    {
        var laneBiasY = roleId switch
        {
            RoleId.LG or RoleId.LT => -12f,
            RoleId.RG or RoleId.RT => +12f,
            RoleId.OC => 0f,
            _ => 0f,
        };

        var desired = blockerPos;
        desired.Y += blockTarget.Assignment switch
        {
            BlockAssignmentType.GapLeft => -10f,
            BlockAssignmentType.GapRight => +10f,
            BlockAssignmentType.PullLeft => -18f,
            BlockAssignmentType.PullRight => +18f,
            BlockAssignmentType.SecondLevel => laneBiasY * 1.2f,
            _ => laneBiasY,
        };

        var bestId = -1;
        var bestScore = float.PositiveInfinity;

        foreach (var defender in defenders)
        {
            if (defender.EntityId == carrierId)
                continue;

            var defPos = defender.Position;
            var d = defPos - desired;
            var distSq = d.LengthSquared();
            var lateralBias = MathF.Abs(defPos.Y - desired.Y) * 3f;
            var levelBias = blockTarget.Assignment == BlockAssignmentType.SecondLevel
                ? MathF.Abs(defPos.X - blockerPos.X) * 0.35f
                : MathF.Abs(defPos.X - blockerPos.X) * 0.10f;
            var preferredPenalty = ComputePreferredDefenderPenalty(blockTarget.PreferredDefenderKey, blockerSlot, blockerPos, defender);
            var rolePenalty = ComputeRolePenalty(blockTarget.Assignment, defender);
            var occupiedPenalty = reservedTargets.TryGetValue(defender.EntityId, out var reservedCount)
                ? (preferredPenalty <= 6f ? reservedCount * 24f : reservedCount * 140f)
                : 0f;
            var rushBonus = blockTarget.Assignment == BlockAssignmentType.SecondLevel || !defender.HasRush
                ? 0f
                : -18f;
            var failedPenaltyRelief = blockTarget.FailedEngagements > 0 && defender.HasRush ? -6f * blockTarget.FailedEngagements : 0f;
            var score = distSq + lateralBias + levelBias + preferredPenalty + rolePenalty + occupiedPenalty + rushBonus + failedPenaltyRelief + (defender.EntityId * 0.0001f);

            if (score < bestScore)
            {
                bestScore = score;
                bestId = defender.EntityId;
            }
        }

        return bestId;
    }

    private static float ComputeRolePenalty(BlockAssignmentType assignment, in DefenderProfile defender)
    {
        if (assignment == BlockAssignmentType.SecondLevel)
        {
            return defender.RoleKind switch
            {
                PlayerRoleKind.LB => 0f,
                PlayerRoleKind.DB => 20f,
                _ => 110f,
            };
        }

        return defender.RoleKind switch
        {
            PlayerRoleKind.DL => 0f,
            PlayerRoleKind.LB => 26f,
            PlayerRoleKind.DB => 90f,
            _ => 45f,
        };
    }

    private static float ComputePreferredDefenderPenalty(string preferredKey, string blockerSlot, Vector2 blockerPos, in DefenderProfile defender)
    {
        if (string.IsNullOrWhiteSpace(preferredKey))
            return 0f;

        var key = preferredKey.Trim().ToUpperInvariant();
        var slot = (defender.Slot ?? string.Empty).Trim().ToUpperInvariant();
        var slotDeltaY = defender.Position.Y - blockerPos.Y;
        var onRight = slotDeltaY > 0f;
        var onLeft = slotDeltaY < 0f;

        return key switch
        {
            "RE" => slot == "DE-R" ? 0f : defender.RoleKind == PlayerRoleKind.DL && onRight ? 18f : 90f,
            "LE" => slot == "DE-L" ? 0f : defender.RoleKind == PlayerRoleKind.DL && onLeft ? 18f : 90f,
            "NT" => slot.StartsWith("DT", StringComparison.Ordinal) ? MathF.Abs(slotDeltaY) * 0.9f : 120f,
            "RILB" => slot == "MLB" ? 6f : slot == "LB-R" ? 0f : defender.RoleKind == PlayerRoleKind.LB ? 28f : 120f,
            "LILB" => slot == "MLB" ? 6f : slot == "LB-L" ? 0f : defender.RoleKind == PlayerRoleKind.LB ? 28f : 120f,
            "ROLB" => slot == "LB-R" ? 0f : slot == "MLB" ? 22f : defender.RoleKind == PlayerRoleKind.LB && onRight ? 28f : 130f,
            "LOLB" => slot == "LB-L" ? 0f : slot == "MLB" ? 22f : defender.RoleKind == PlayerRoleKind.LB && onLeft ? 28f : 130f,
            "RCB" => slot == "CB-R" ? 0f : defender.RoleKind == PlayerRoleKind.DB && onRight ? 18f : 140f,
            "LCB" => slot == "CB-L" ? 0f : defender.RoleKind == PlayerRoleKind.DB && onLeft ? 18f : 140f,
            _ => slot.Contains(key, StringComparison.Ordinal) ? 0f : 36f,
        };
    }

    private static void ClearDoubleTeamFlags(World world, IReadOnlyList<int> offenseEntityIds)
    {
        var allow = new HashSet<int>(offenseEntityIds);
        var q = new QueryDescription().WithAll<BlockTarget>();
        world.Query(in q, (Entity e, ref BlockTarget bt) =>
        {
            if (!allow.Contains(e.Id))
                return;

            bt.IsDoubleTeam = false;
        });
    }

    private static void ApplyDoubleTeams(World world, IReadOnlyList<int> offenseEntityIds)
    {
        var allow = new HashSet<int>(offenseEntityIds);
        var counts = new Dictionary<int, List<int>>();

        var q = new QueryDescription().WithAll<BlockTarget>();
        world.Query(in q, (Entity e, ref BlockTarget bt) =>
        {
            if (!allow.Contains(e.Id))
                return;
            if (!bt.IsEngaged || bt.EngagedEntityId < 0)
                return;

            if (!counts.TryGetValue(bt.EngagedEntityId, out var list))
            {
                list = new List<int>(capacity: 2);
                counts[bt.EngagedEntityId] = list;
            }

            list.Add(e.Id);
        });

        foreach (var kv in counts)
        {
            if (kv.Value.Count < 2)
                continue;

            foreach (var blockerId in kv.Value)
                SetDoubleTeamFlag(world, blockerId, true);

            EnsureSpeedModifier(world, kv.Key, DOUBLE_TEAM_DEFENDER_SPEED_MULT, DOUBLE_TEAM_REFRESH_SECONDS);
        }
    }

    private static void SetDoubleTeamFlag(World world, int entityId, bool isDoubleTeam)
    {
        var q = new QueryDescription().WithAll<BlockTarget>();
        world.Query(in q, (Entity e, ref BlockTarget bt) =>
        {
            if (e.Id != entityId)
                return;

            bt.IsDoubleTeam = isDoubleTeam;
        });
    }

    private static void EnsureSpeedModifier(World world, int entityId, float multiplier, float timerSeconds)
    {
        var q = new QueryDescription().WithAll<SpeedModifier>();
        world.Query(in q, (Entity e, ref SpeedModifier sm) =>
        {
            if (e.Id != entityId)
                return;

            sm.MaxSpeedMultiplier = MathF.Min(sm.MaxSpeedMultiplier <= 0f ? 1f : sm.MaxSpeedMultiplier, multiplier);
            sm.TimerSeconds = MathF.Max(sm.TimerSeconds, timerSeconds);
        });
    }

    private static HashSet<int> GatherActiveRouteRunners(World world, IReadOnlyList<int> offenseEntityIds)
    {
        var allow = new HashSet<int>(offenseEntityIds);
        var result = new HashSet<int>();
        var q = new QueryDescription().WithAll<RouteFollow>();
        world.Query(in q, (Entity e, ref RouteFollow route) =>
        {
            if (!allow.Contains(e.Id) || route.Completed)
                return;

            result.Add(e.Id);
        });

        return result;
    }

    private static List<DefenderProfile> GatherDefenderProfiles(World world, IReadOnlyList<int> defenseEntityIds)
    {
        var allow = new HashSet<int>(defenseEntityIds);
        var result = new List<DefenderProfile>(allow.Count);
        var q = new QueryDescription().WithAll<Position, Role, Team>();
        world.Query(in q, (Entity e, ref Position position, ref Role role, ref Team team) =>
        {
            if (!allow.Contains(e.Id) || team.IsOffense)
                return;

            var slot = e.Has<PlayerRole>() ? e.Get<PlayerRole>().Slot ?? string.Empty : string.Empty;
            var roleKind = e.Has<PlayerRole>() ? e.Get<PlayerRole>().Role : RoleKindFromRoleId(role.Id);
            result.Add(new DefenderProfile(e.Id, position.Value, roleKind, slot, e.Has<Rush>()));
        });

        return result;
    }

    private static PlayerRoleKind RoleKindFromRoleId(RoleId roleId)
        => roleId switch
        {
            RoleId.DL1 or RoleId.DL2 or RoleId.DL3 or RoleId.DL4 => PlayerRoleKind.DL,
            RoleId.LB1 or RoleId.LB2 or RoleId.LB3 or RoleId.LB4 => PlayerRoleKind.LB,
            RoleId.CB1 or RoleId.CB2 or RoleId.S1 or RoleId.S2 => PlayerRoleKind.DB,
            _ => PlayerRoleKind.Unknown,
        };

    private static int FindBallOwner(World world, int ballEntityId)
    {
        var owner = -1;
        var found = false;

        var q = new QueryDescription().WithAll<Ball>();
        world.Query(in q, (Entity e, ref Ball b) =>
        {
            if (found)
                return;
            if (e.Id != ballEntityId)
                return;

            if (b.State == BallState.Held)
                owner = b.OwnerEntityId;

            found = true;
        });

        return owner;
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

    private static bool TryGet(World world, int entityId, out BlockTarget bt, out Behavior beh, out Position pos, out Role role, out PlayerRole playerRole)
    {
        bt = default;
        beh = default;
        pos = default;
        role = default;
        playerRole = default;

        var found = false;
        var btLocal = default(BlockTarget);
        var bLocal = default(Behavior);
        var pLocal = default(Position);
        var rLocal = default(Role);
        var prLocal = default(PlayerRole);

        var q = new QueryDescription().WithAll<BlockTarget, Behavior, Position, Role>();
        world.Query(in q, (Entity e, ref BlockTarget bt0, ref Behavior b0, ref Position p0, ref Role r0) =>
        {
            if (found)
                return;
            if (e.Id != entityId)
                return;

            btLocal = bt0;
            bLocal = b0;
            pLocal = p0;
            rLocal = r0;
            prLocal = e.Has<PlayerRole>() ? e.Get<PlayerRole>() : PlayerRole.Create(PlayerRoleKind.Unknown);
            found = true;
        });

        if (!found)
            return false;

        bt = btLocal;
        beh = bLocal;
        pos = pLocal;
        role = rLocal;
        playerRole = prLocal;
        return true;
    }

    private static void Store(World world, int entityId, BlockTarget bt, Behavior beh)
    {
        WithEntity(world, entityId, e =>
        {
            e.Set(bt);
            e.Set(beh);
        });
    }

    private static void WithEntity(World world, int entityId, Action<Entity> action)
    {
        var q = new QueryDescription().WithAll<Position>();
        world.Query(in q, (Entity e, ref Position _) =>
        {
            if (e.Id == entityId)
                action(e);
        });
    }

    private static Vector2 ComputeApproachOffset(RoleId roleId, in BlockTarget blockTarget, Vector2 blockerPos, Vector2 targetPos)
    {
        var laneOffset = roleId switch
        {
            RoleId.LT or RoleId.LG => -3.5f,
            RoleId.RT or RoleId.RG or RoleId.TE => 3.5f,
            _ => 0f,
        };

        if (blockTarget.Assignment == BlockAssignmentType.SecondLevel)
            laneOffset *= 1.5f;

        var desiredY = targetPos.Y + laneOffset;
        var clampY = MathHelper.Clamp(desiredY, blockerPos.Y - 12f, blockerPos.Y + 12f);
        var desiredX = targetPos.X - 1.5f;
        return new Vector2(desiredX - targetPos.X, clampY - targetPos.Y);
    }

    private static bool TryGetDefenderProfile(int entityId, IReadOnlyList<DefenderProfile> defenders, out DefenderProfile profile)
    {
        foreach (var defender in defenders)
        {
            if (defender.EntityId == entityId)
            {
                profile = defender;
                return true;
            }
        }

        profile = default;
        return false;
    }

    private readonly record struct DefenderProfile(int EntityId, Vector2 Position, PlayerRoleKind RoleKind, string Slot, bool HasRush);
}
