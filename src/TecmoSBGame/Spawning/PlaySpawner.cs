using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using TecmoSB;
using TecmoSBGame.Components;

namespace TecmoSBGame.Spawning;

/// <summary>
/// Chooses an offensive + defensive play deterministically (for now) and attaches
/// route/assignment components to spawned entities.
///
/// This is intentionally lightweight and produces placeholder assignments that future
/// systems (PlayExecutionSystem / AI) can consume.
/// </summary>
public sealed class PlaySpawner
{
    public sealed record SpawnedAssignment(
        int EntityId,
        int TeamIndex,
        bool IsOffense,
        PlayerRole Role,
        string Slot,
        string Summary);

    public sealed record SpawnedPlay(
        string OffensivePlayName,
        string OffensiveSlot,
        string OffensiveFormationId,
        int OffensivePlayNumber,
        string DefensiveCallId,
        IReadOnlyList<SpawnedAssignment> Assignments);

    /// <summary>
    /// Deterministically chooses an offensive play from the playlist and a defensive call
    /// from the defense YAML, then attaches per-entity assignment components.
    /// </summary>
    /// <remarks>
    /// Inputs are the spawned entities from a formation (offense and defense).
    /// This avoids needing an ECS query utility at this stage.
    /// </remarks>
    public SpawnedPlay Spawn(
        World world,
        PlayListConfig playList,
        DefensePlayConfig defensePlays,
        IReadOnlyList<int> offenseEntityIds,
        IReadOnlyList<int> defenseEntityIds,
        PlayEntry? selectedOffensivePlay = null,
        string? selectedDefensiveCallId = null)
    {
        if (world is null) throw new ArgumentNullException(nameof(world));
        if (playList is null) throw new ArgumentNullException(nameof(playList));
        if (defensePlays is null) throw new ArgumentNullException(nameof(defensePlays));
        if (offenseEntityIds is null) throw new ArgumentNullException(nameof(offenseEntityIds));
        if (defenseEntityIds is null) throw new ArgumentNullException(nameof(defenseEntityIds));

        var offensivePlay = selectedOffensivePlay ?? ChooseOffensivePlay(playList);
        var defensiveExecution = ChooseDefensiveExecution(defensePlays, selectedDefensiveCallId);

        var playNumber = offensivePlay.PlayNumbers.Count > 0 ? offensivePlay.PlayNumbers[0] : 0;

        // Attach assignments
        var assignments = new List<SpawnedAssignment>(offenseEntityIds.Count + defenseEntityIds.Count);

        int qbEntityId = -1;

        foreach (var id in offenseEntityIds)
        {
            var e = world.GetEntity(id);
            var role = e.Get<PlayerRoleComponent>()?.Role ?? PlayerRole.Unknown;
            var slot = e.Get<PlayerRoleComponent>()?.Slot ?? "";
            var teamIndex = e.Get<TeamComponent>()?.TeamIndex ?? -1;

            if (role == PlayerRole.QB)
                qbEntityId = id;

            // Ensure play metadata is present.
            AttachOrUpdatePlayCall(e, offensivePlay, defensiveExecution.Id);

            var oa = new OffensiveAssignmentComponent();
            FillOffensiveAssignment(world, id, role, slot, oa);
            e.Attach(oa);

            // ROUTE-* integration: translate the high-level waypoints into a frame-timed RouteComponent.
            // This is a scaffold until we import real ROM route timing tables.
            AttachRouteIfNeeded(world, id, role, slot, oa);

            assignments.Add(new SpawnedAssignment(
                EntityId: id,
                TeamIndex: teamIndex,
                IsOffense: true,
                Role: role,
                Slot: slot,
                Summary: DescribeOffense(oa)));
        }

        // QB AI: attach a brain + deterministic read order derived from eligible receivers.
        if (qbEntityId != -1)
        {
            var qb = world.GetEntity(qbEntityId);
            if (!qb.Has<QbBrainComponent>())
                qb.Attach(new QbBrainComponent());

            var brain = qb.Get<QbBrainComponent>();
            brain.Dropback = InferDropbackType(offensivePlay);

            // Priority order: WR1 -> WR2 -> TE -> RB -> remaining WR/TE/RB (by entity id).
            var eligibles = offenseEntityIds
                .Select(id => (id, prc: world.GetEntity(id).Get<PlayerRoleComponent>()))
                .Where(x => x.prc is not null && (x.prc.Role is PlayerRole.WR or PlayerRole.TE or PlayerRole.RB))
                .Select(x => (x.id, role: x.prc!.Role, slot: x.prc!.Slot ?? string.Empty))
                .ToList();

            static int Priority(PlayerRole role, string slot)
            {
                var s = (slot ?? string.Empty).Trim().ToUpperInvariant();
                if (role == PlayerRole.WR && (s == "WR1" || s.Contains("WR1"))) return 0;
                if (role == PlayerRole.WR && (s == "WR2" || s.Contains("WR2"))) return 1;
                if (role == PlayerRole.TE) return 2;
                if (role == PlayerRole.RB) return 3;
                if (role == PlayerRole.WR) return 4;
                return 5;
            }

            brain.ReadOrder.Clear();
            foreach (var r in eligibles.OrderBy(x => Priority(x.role, x.slot)).ThenBy(x => x.id))
                brain.ReadOrder.Add(r.id);

            brain.CurrentReadIndex = 0;
            brain.ReadTimer = 0;
            brain.ThrowDecisionMade = false;
            brain.TargetReceiverId = -1;
        }

        // Defensive assignments benefit from knowing offensive skill positions.
        var receivers = offenseEntityIds
            .Select(id => (id, role: world.GetEntity(id).Get<PlayerRoleComponent>()?.Role ?? PlayerRole.Unknown))
            .Where(x => x.role is PlayerRole.WR or PlayerRole.TE or PlayerRole.RB)
            .Select(x => x.id)
            .ToList();

        var qbId = offenseEntityIds.FirstOrDefault(id => world.GetEntity(id).Get<PlayerRoleComponent>()?.Role == PlayerRole.QB);

        var receiverIdx = 0;
        for (var defIndex = 0; defIndex < defenseEntityIds.Count; defIndex++)
        {
            var id = defenseEntityIds[defIndex];
            var e = world.GetEntity(id);
            var role = e.Get<PlayerRoleComponent>()?.Role ?? PlayerRole.Unknown;
            var slot = e.Get<PlayerRoleComponent>()?.Slot ?? "";
            var teamIndex = e.Get<TeamComponent>()?.TeamIndex ?? -1;

            AttachOrUpdatePlayCall(e, offensivePlay, defensiveExecution.Id);

            var da = new DefensiveAssignmentComponent();
            FillDefensiveAssignment(id, role, slot, qbId, receivers, ref receiverIdx, da);
            e.Attach(da);

            // Data-driven rush assignments (bank4 DefensePlayData) are scaffolded.
            // (Gap-level roles are not wired through all YAML/config paths yet.)

            if (da.Kind == DefensiveAssignmentKind.PassRush && !e.Has<RushComponent>())
            {
                e.Attach(new RushComponent
                {
                    TargetGap = InferDefaultRushGapFromSlot(slot),
                    IsContain = slot.Contains("E", StringComparison.OrdinalIgnoreCase), // ends contain by default
                    Type = RushType.Power,
                });
            }

            assignments.Add(new SpawnedAssignment(
                EntityId: id,
                TeamIndex: teamIndex,
                IsOffense: false,
                Role: role,
                Slot: slot,
                Summary: DescribeDefense(da)));
        }

        return new SpawnedPlay(
            OffensivePlayName: offensivePlay.Name,
            OffensiveSlot: offensivePlay.Slot,
            OffensiveFormationId: offensivePlay.Formation,
            OffensivePlayNumber: playNumber,
            DefensiveCallId: defensiveExecution.Id,
            Assignments: assignments);
    }

    private static PlayEntry ChooseOffensivePlay(PlayListConfig playList)
    {
        // Deterministic pick:
        // 1) first "Pass" slot play (gives us routes to attach)
        // 2) otherwise first play in list
        if (playList.PlayList.Count == 0)
            return new PlayEntry(Name: "(no plays)", Slot: "", Formation: "", PlayNumbers: Array.Empty<int>(), Defense: Array.Empty<string>());

        if (playList.PlayList.Count == 0)
            return new PlayEntry(Name: "(no plays)", Slot: "", Formation: "", PlayNumbers: Array.Empty<int>(), Defense: Array.Empty<string>());

        var pass = playList.PlayList.FirstOrDefault(p => (p.Slot ?? string.Empty).StartsWith("Pass", StringComparison.OrdinalIgnoreCase));
        return pass ?? playList.PlayList.First();
    }

    private static DefensiveExecution ChooseDefensiveExecution(DefensePlayConfig defensePlays, string? preferredId)
    {
        if (!string.IsNullOrWhiteSpace(preferredId))
        {
            var match = defensePlays.DefensiveExecutions.FirstOrDefault(d => string.Equals(d.Id, preferredId, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
        }

        // Deterministic pick: first defensive execution in YAML.
        if (defensePlays.DefensiveExecutions.Count > 0)
            return defensePlays.DefensiveExecutions.First();

        // Graceful fallback.
        return new DefensiveExecution(Id: "DEF-UNKNOWN", Description: "(missing defense YAML)", PlayerReactions: Array.Empty<PlayerReactionRef>());
    }

    private static DropbackType InferDropbackType(PlayEntry offense)
    {
        // Until playdata YAML contains an explicit dropback type, infer from the slot/name.
        // Keep deterministic and conservative: most Tecmo pass plays are 5-step.
        var slot = (offense.Slot ?? string.Empty).Trim().ToUpperInvariant();
        var name = (offense.Name ?? string.Empty).Trim().ToUpperInvariant();

        if (slot.Contains("SHOTGUN") || name.Contains("SHOTGUN"))
            return DropbackType.Shotgun;

        if (slot.Contains("ROLLOUTL") || name.Contains("ROLLOUTL") || name.Contains("ROLL OUT L"))
            return DropbackType.RolloutLeft;
        if (slot.Contains("ROLLOUTR") || name.Contains("ROLLOUTR") || name.Contains("ROLL OUT R"))
            return DropbackType.RolloutRight;

        if (slot.Contains("3") || name.Contains("3-STEP") || name.Contains("3 STEP"))
            return DropbackType.ThreeStep;
        if (slot.Contains("7") || name.Contains("7-STEP") || name.Contains("7 STEP"))
            return DropbackType.SevenStep;

        return DropbackType.FiveStep;
    }

    private static void AttachOrUpdatePlayCall(MonoGame.Extended.Entities.Entity e, PlayEntry offense, string defenseId)
    {
        if (!e.Has<PlayCallInfoComponent>())
            e.Attach(new PlayCallInfoComponent());

        var pc = e.Get<PlayCallInfoComponent>();
        pc.OffensivePlayName = offense.Name;
        pc.OffensivePlaySlot = offense.Slot;
        pc.OffensiveFormationId = offense.Formation;
        pc.DefensiveCallId = defenseId;
    }

    private static void FillOffensiveAssignment(World world, int entityId, PlayerRole role, string slot, OffensiveAssignmentComponent oa)
    {
        oa.Kind = role switch
        {
            PlayerRole.QB => OffensiveAssignmentKind.Quarterback,
            PlayerRole.WR or PlayerRole.TE => OffensiveAssignmentKind.RouteRunner,
            PlayerRole.RB => OffensiveAssignmentKind.RouteRunner,
            PlayerRole.OL => OffensiveAssignmentKind.Blocker,
            _ => OffensiveAssignmentKind.None,
        };

        var pos = world.GetEntity(entityId).Get<PositionComponent>()?.Position ?? Vector2.Zero;

        switch (oa.Kind)
        {
            case OffensiveAssignmentKind.Quarterback:
                // Simple deterministic dropback target.
                oa.Notes = "dropback";
                oa.RouteWaypoints.Clear();
                oa.RouteWaypoints.Add(pos + new Vector2(-18, 0));
                break;

            case OffensiveAssignmentKind.RouteRunner:
                oa.Notes = string.IsNullOrWhiteSpace(slot) ? "route" : $"route:{slot}";
                oa.RouteWaypoints.Clear();
                foreach (var wp in BuildSimpleRoute(pos, role, slot))
                    oa.RouteWaypoints.Add(wp);
                break;

            case OffensiveAssignmentKind.Blocker:
                oa.Notes = string.IsNullOrWhiteSpace(slot) ? "block" : $"block:{slot}";
                oa.TargetEntityId = -1;
                oa.RouteWaypoints.Clear();
                break;
        }
    }

    private static IEnumerable<Vector2> BuildSimpleRoute(Vector2 start, PlayerRole role, string slot)
    {
        // Keep it legible. 2-3 points max.
        // Assume offense is moving +X.
        var s = (slot ?? string.Empty).Trim().ToUpperInvariant();

        // Default: straight go.
        var a = start + new Vector2(28, 0);
        var b = start + new Vector2(60, 0);

        // Spread by slot side.
        var side = s.Contains('1') || s.Contains('L') ? -1 : (s.Contains('2') || s.Contains('R') ? 1 : 0);

        if (role == PlayerRole.TE)
        {
            // Short out.
            a = start + new Vector2(18, 0);
            b = start + new Vector2(28, 18 * (side == 0 ? 1 : side));
        }
        else if (role == PlayerRole.RB)
        {
            // Flare/swing.
            a = start + new Vector2(6, 16 * (side == 0 ? 1 : side));
            b = start + new Vector2(18, 32 * (side == 0 ? 1 : side));
        }
        else if (role == PlayerRole.WR)
        {
            // Simple out for WR1/WR2 and a deeper go otherwise.
            if (s.Contains("WR1") || s.Contains("WR2") || s == "WR")
            {
                a = start + new Vector2(34, 0);
                b = start + new Vector2(52, 28 * (side == 0 ? 1 : side));
            }
            else
            {
                a = start + new Vector2(30, 0);
                b = start + new Vector2(72, 0);
            }
        }

        yield return a;
        yield return b;
    }

    private static void FillDefensiveAssignment(
        int entityId,
        PlayerRole role,
        string slot,
        int qbId,
        List<int> receivers,
        ref int receiverIdx,
        DefensiveAssignmentComponent da)
    {
        da.Kind = role switch
        {
            PlayerRole.DL => DefensiveAssignmentKind.PassRush,
            PlayerRole.LB => DefensiveAssignmentKind.Pursuit,
            PlayerRole.DB => DefensiveAssignmentKind.ManCoverage,
            _ => DefensiveAssignmentKind.None,
        };

        da.TargetEntityId = -1;
        da.Anchor = Vector2.Zero;
        da.Notes = string.IsNullOrWhiteSpace(slot) ? "" : slot;

        switch (da.Kind)
        {
            case DefensiveAssignmentKind.PassRush:
                da.TargetEntityId = qbId;
                da.Notes = string.IsNullOrWhiteSpace(slot) ? "rush" : $"rush:{slot}";
                break;

            case DefensiveAssignmentKind.Pursuit:
                da.TargetEntityId = qbId;
                da.Notes = string.IsNullOrWhiteSpace(slot) ? "pursuit" : $"pursuit:{slot}";
                break;

            case DefensiveAssignmentKind.ManCoverage:
                if (receivers.Count > 0)
                {
                    da.TargetEntityId = receivers[receiverIdx % receivers.Count];
                    receiverIdx++;
                }
                da.Notes = string.IsNullOrWhiteSpace(slot) ? "man" : $"man:{slot}";
                break;
        }
    }

    private static void AttachRushComponentIfPresent(
        World world,
        int entityId,
        DefensiveExecution defensiveExecution,
        DefensePlayConfig defensePlays,
        int defenseIndex)
    {
        // DefensiveExecution.PlayerReactions is 11 entries of (Index, ReactionId).
        // We treat defenseIndex (spawn order) as the player index.
        var reactionRef = defensiveExecution.PlayerReactions.FirstOrDefault(r => r.Index == defenseIndex);
        if (reactionRef.ReactionId is null)
            return;

        var reaction = defensePlays.DefensePlayerReactions.FirstOrDefault(r => string.Equals(r.Id, reactionRef.ReactionId, StringComparison.OrdinalIgnoreCase));
        if (reaction is null)
            return;

        if (!TryParseRushRole(reaction.Role, out var gap, out var contain, out var stunt, out var stuntDelay, out var stuntGap))
            return;

        var e = world.GetEntity(entityId);
        if (!e.Has<RushComponent>())
            e.Attach(new RushComponent());

        var rc = e.Get<RushComponent>();
        rc.TargetGap = gap;
        rc.IsContain = contain;

        // Default rush move type until per-play data is surfaced: DL power, LB swim.
        rc.Type = rc.IsContain ? RushType.Swim : RushType.Power;

        rc.IsStunt = stunt;
        rc.StuntDelayFrames = stuntDelay;
        rc.StuntTargetGap = stuntGap;

        rc.GapReached = false;
        rc.Engaged = false;
        rc.EngagedBlockerId = -1;
    }

    private static RushGap InferDefaultRushGapFromSlot(string slot)
    {
        // Stable fallback mapping for placeholder defenses until bank4 roles include gap details.
        var s = (slot ?? string.Empty).Trim().ToUpperInvariant();

        // Ends
        if (s is "LE" or "LDE") return RushGap.ContainLeft;
        if (s is "RE" or "RDE") return RushGap.ContainRight;

        // Interior
        if (s.Contains("NT", StringComparison.Ordinal)) return RushGap.ALeft;
        if (s.Contains("DT", StringComparison.Ordinal)) return RushGap.ARight;

        return RushGap.BLeft;
    }

    private static bool TryParseRushRole(
        string role,
        out RushGap gap,
        out bool contain,
        out bool stunt,
        out int stuntDelayFrames,
        out RushGap stuntTargetGap)
    {
        gap = RushGap.ALeft;
        contain = false;
        stunt = false;
        stuntDelayFrames = 0;
        stuntTargetGap = RushGap.ALeft;

        if (string.IsNullOrWhiteSpace(role))
            return false;

        var s = role.Trim().ToUpperInvariant();

        // Simple bank4-style roles: RUSH-1..RUSH-6
        // Map to left/right A/B/C gaps.
        if (s.StartsWith("RUSH-", StringComparison.Ordinal))
        {
            var numStr = s[5..];
            if (int.TryParse(numStr, out var n))
            {
                gap = n switch
                {
                    1 => RushGap.ALeft,
                    2 => RushGap.BLeft,
                    3 => RushGap.CLeft,
                    4 => RushGap.ARight,
                    5 => RushGap.BRight,
                    6 => RushGap.CRight,
                    _ => RushGap.ALeft,
                };
                return true;
            }
        }

        // Contain variants (if authored in YAML): CONTAIN-L / CONTAIN-R
        if (s is "CONTAIN-L" or "CONTAINLEFT")
        {
            gap = RushGap.ContainLeft;
            contain = true;
            return true;
        }

        if (s is "CONTAIN-R" or "CONTAINRIGHT")
        {
            gap = RushGap.ContainRight;
            contain = true;
            return true;
        }

        // Stunt variants (optional authoring): STUNT:<from>-><to>@<delay>
        // Example: STUNT:RUSH-2->RUSH-5@18
        if (s.StartsWith("STUNT:", StringComparison.Ordinal))
        {
            // Very small parser; if it fails, ignore.
            var payload = s[6..];
            var parts = payload.Split('@', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 1)
            {
                var map = parts[0].Split("->", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (map.Length == 2
                    && TryParseRushRole(map[0], out gap, out contain, out _, out _, out _)
                    && TryParseRushRole(map[1], out stuntTargetGap, out var contain2, out _, out _, out _))
                {
                    contain = contain || contain2;
                    stunt = true;
                    stuntDelayFrames = 18;
                    if (parts.Length >= 2 && int.TryParse(parts[1], out var d))
                        stuntDelayFrames = Math.Clamp(d, 0, 120);
                    return true;
                }
            }
        }

        return false;
    }

    private static void AttachRouteIfNeeded(World world, int entityId, PlayerRole role, string slot, OffensiveAssignmentComponent oa)
    {
        if (oa.Kind is not (OffensiveAssignmentKind.RouteRunner or OffensiveAssignmentKind.Quarterback))
            return;

        var e = world.GetEntity(entityId);
        var origin = e.Get<PositionComponent>()?.Position ?? Vector2.Zero;

        // If the entity already has a RouteComponent (e.g., future true YAML), don't overwrite.
        if (e.Has<RouteComponent>())
            return;

        // Build nodes from current waypoint scaffold.
        // Node0: break point after StemFrames.
        // Node1: "run" extended to keep moving in the last segment direction.
        var nodes = new List<RouteNode>();

        // Default stem timing: until ROM tables are imported.
        var stemFrames = role switch
        {
            PlayerRole.WR => 22,
            PlayerRole.TE => 18,
            PlayerRole.RB => 14,
            PlayerRole.QB => 16,
            _ => 18,
        };

        if (oa.RouteWaypoints.Count == 0)
        {
            // No explicit target. Avoid attaching a dead route.
            return;
        }

        // First point: treat as a break/landmark point.
        var a = oa.RouteWaypoints[0];
        nodes.Add(new RouteNode
        {
            Offset = a - origin,
            MinFrames = stemFrames,
            Action = "RUN",
        });

        // Second point (if present) becomes the direction basis.
        Vector2 b;
        if (oa.RouteWaypoints.Count > 1)
            b = oa.RouteWaypoints[1];
        else
            b = a + new Vector2(40, 0);

        // Extend the final segment so the runner keeps moving.
        var dir = b - a;
        if (dir.LengthSquared() < 0.0001f)
            dir = new Vector2(1, 0);
        dir.Normalize();

        var extendedEnd = a + dir * 220f;
        nodes.Add(new RouteNode
        {
            Offset = extendedEnd - origin,
            MinFrames = int.MaxValue,
            Action = "RUN",
        });

        // BaseSpeed (units/tick at MS=69). Until ROM values are imported, use a stable constant.
        // The RouteFollowSystem will scale this by player MS.
        const float baseRouteSpeed = 3.65f;

        e.Attach(new RouteComponent
        {
            RouteType = string.IsNullOrWhiteSpace(slot) ? role.ToString() : slot,
            Nodes = nodes,
            CurrentNodeIndex = 0,
            FrameCounter = 0,
            RouteComplete = false,
            IsSitting = false,
            StemFrames = stemFrames,
            BaseSpeed = baseRouteSpeed,
        });

        // Ensure the Behavior state is set so MovementSystem will follow the route targets.
        if (e.Get<BehaviorComponent>() is { } behavior)
        {
            behavior.State = BehaviorState.RunningRoute;
            behavior.TargetPosition = a;
        }
    }

    private static string DescribeOffense(OffensiveAssignmentComponent oa)
    {
        return oa.Kind switch
        {
            OffensiveAssignmentKind.Quarterback => $"QB {oa.Notes} -> ({oa.RouteWaypoints.FirstOrDefault().X:0.0},{oa.RouteWaypoints.FirstOrDefault().Y:0.0})",
            OffensiveAssignmentKind.RouteRunner => $"route {oa.Notes} pts={oa.RouteWaypoints.Count}",
            OffensiveAssignmentKind.Blocker => $"block {oa.Notes}",
            _ => "(none)",
        };
    }

    private static string DescribeDefense(DefensiveAssignmentComponent da)
    {
        return da.Kind switch
        {
            DefensiveAssignmentKind.PassRush => $"rush target={da.TargetEntityId}",
            DefensiveAssignmentKind.Pursuit => $"pursuit target={da.TargetEntityId}",
            DefensiveAssignmentKind.ManCoverage => $"man target={da.TargetEntityId}",
            DefensiveAssignmentKind.ZoneCoverage => "zone",
            _ => "(none)",
        };
    }
}
