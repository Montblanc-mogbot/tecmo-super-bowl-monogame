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
        IReadOnlyList<int> defenseEntityIds)
    {
        if (world is null) throw new ArgumentNullException(nameof(world));
        if (playList is null) throw new ArgumentNullException(nameof(playList));
        if (defensePlays is null) throw new ArgumentNullException(nameof(defensePlays));
        if (offenseEntityIds is null) throw new ArgumentNullException(nameof(offenseEntityIds));
        if (defenseEntityIds is null) throw new ArgumentNullException(nameof(defenseEntityIds));

        var offensivePlay = ChooseOffensivePlay(playList);
        var defensiveExecution = ChooseDefensiveExecution(defensePlays);

        var playNumber = offensivePlay.PlayNumbers.Count > 0 ? offensivePlay.PlayNumbers[0] : 0;

        // Attach assignments
        var assignments = new List<SpawnedAssignment>(offenseEntityIds.Count + defenseEntityIds.Count);

        foreach (var id in offenseEntityIds)
        {
            var e = world.GetEntity(id);
            var role = e.Get<PlayerRoleComponent>()?.Role ?? PlayerRole.Unknown;
            var slot = e.Get<PlayerRoleComponent>()?.Slot ?? "";
            var teamIndex = e.Get<TeamComponent>()?.TeamIndex ?? -1;

            // Ensure play metadata is present.
            AttachOrUpdatePlayCall(e, offensivePlay, defensiveExecution.Id);

            var oa = new OffensiveAssignmentComponent();
            FillOffensiveAssignment(world, id, role, slot, oa);
            e.Attach(oa);

            assignments.Add(new SpawnedAssignment(
                EntityId: id,
                TeamIndex: teamIndex,
                IsOffense: true,
                Role: role,
                Slot: slot,
                Summary: DescribeOffense(oa)));
        }

        // Defensive assignments benefit from knowing offensive skill positions.
        var receivers = offenseEntityIds
            .Select(id => (id, role: world.GetEntity(id).Get<PlayerRoleComponent>()?.Role ?? PlayerRole.Unknown))
            .Where(x => x.role is PlayerRole.WR or PlayerRole.TE or PlayerRole.RB)
            .Select(x => x.id)
            .ToList();

        var qbId = offenseEntityIds.FirstOrDefault(id => world.GetEntity(id).Get<PlayerRoleComponent>()?.Role == PlayerRole.QB);

        var receiverIdx = 0;
        foreach (var id in defenseEntityIds)
        {
            var e = world.GetEntity(id);
            var role = e.Get<PlayerRoleComponent>()?.Role ?? PlayerRole.Unknown;
            var slot = e.Get<PlayerRoleComponent>()?.Slot ?? "";
            var teamIndex = e.Get<TeamComponent>()?.TeamIndex ?? -1;

            AttachOrUpdatePlayCall(e, offensivePlay, defensiveExecution.Id);

            var da = new DefensiveAssignmentComponent();
            FillDefensiveAssignment(id, role, slot, qbId, receivers, ref receiverIdx, da);
            e.Attach(da);

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
        var pass = playList.PlayList.FirstOrDefault(p => (p.Slot ?? string.Empty).StartsWith("Pass", StringComparison.OrdinalIgnoreCase));
        return pass ?? playList.PlayList.First();
    }

    private static DefensiveExecution ChooseDefensiveExecution(DefensePlayConfig defensePlays)
    {
        // Deterministic pick: first defensive execution in YAML.
        return defensePlays.DefensiveExecutions.First();
    }

    private static void AttachOrUpdatePlayCall(MonoGame.Extended.Entities.Entity e, PlayEntry offense, string defenseId)
    {
        if (!e.Has<PlayCallComponent>())
            e.Attach(new PlayCallComponent());

        var pc = e.Get<PlayCallComponent>();
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
