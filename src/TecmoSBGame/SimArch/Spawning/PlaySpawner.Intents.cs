using System;
using System.Collections.Generic;
using System.Linq;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSB;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Spawning;

public static partial class PlaySpawner
{
    private static void ApplyMovementIntents(
        World world,
        PlayDataConfig playData,
        IReadOnlyDictionary<string, string>? slotToReactionId,
        IReadOnlyDictionary<string, int> slotToEntityId)
    {
        if (slotToReactionId is null)
            return;

        foreach (var (slot, reactionId) in slotToReactionId)
        {
            if (!slotToEntityId.TryGetValue(slot, out var entityId))
                continue;

            var reaction = playData.PlayerReactions.FirstOrDefault(r => string.Equals(r.Id, reactionId, StringComparison.OrdinalIgnoreCase));
            if (reaction is null)
                continue;

            if (!TryGetMoveBy(reaction, out var dx, out var dy))
                continue;

            // Apply a one-shot "move to relative position" intent.
            var q = new QueryDescription().WithAll<Position, Behavior>();
            world.Query(in q, (Entity e, ref Position pos, ref Behavior beh) =>
            {
                if (e.Id != entityId)
                    return;

                beh.State = BehaviorState.MovingToPosition;
                beh.TargetEntityId = -1;
                beh.TargetPosition = pos.Value + new Vector2(dx, dy);
                beh.StateTimer = 0f;
            });
        }
    }

    private static bool TryGetMoveBy(PlayerReactionScript reaction, out float dx, out float dy)
    {
        dx = 0f;
        dy = 0f;

        foreach (var c in reaction.Commands)
        {
            var cmd = (c.Cmd ?? string.Empty).Trim();
            if (!cmd.Equals("move_by", StringComparison.OrdinalIgnoreCase))
                continue;

            if (c.Params is not { Count: >= 2 })
                continue;

            dx = ParseFloat(c.Params[0]);
            dy = ParseFloat(c.Params[1]);
            return true;
        }

        return false;
    }

    private static IReadOnlyDictionary<string, int> BuildSlotLookup(World world, IReadOnlyList<int> offenseEntityIds)
    {
        var allow = new HashSet<int>(offenseEntityIds);
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var q = new QueryDescription().WithAll<Role>();
        world.Query(in q, (Entity e, ref Role r) =>
        {
            if (!allow.Contains(e.Id))
                return;

            var slot = r.Id switch
            {
                RoleId.QB => "QB",
                RoleId.HB => "HB",
                RoleId.FB => "FB",
                RoleId.WR1 => "WR1",
                RoleId.WR2 => "WR2",
                RoleId.TE => "TE",
                RoleId.OC => "OC",
                RoleId.LG => "LG",
                RoleId.RG => "RG",
                RoleId.LT => "LT",
                RoleId.RT => "RT",
                _ => null,
            };

            if (slot is null)
                return;

            if (!map.ContainsKey(slot))
                map[slot] = e.Id;
        });

        return map;
    }

    // Defensive slots in PlayData are authored like: DE-L / DT-L / DT-R / DE-R / LB-L / MLB / LB-R / CB-L / CB-R / S-L / S-R
    // Our current SimArch defense is a placeholder; we map it deterministically to these keys.
    private static IReadOnlyDictionary<string, int> BuildDefensiveSlotLookup(World world, IReadOnlyList<int> defenseEntityIds)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Deterministic assignment by spawn order.
        // FormationSpawner currently spawns: DL1..DL4, LB1..LB4, CB1..CB2, S1.
        if (defenseEntityIds.Count >= 11)
        {
            map["DE-L"] = defenseEntityIds[0];
            map["DT-L"] = defenseEntityIds[1];
            map["DT-R"] = defenseEntityIds[2];
            map["DE-R"] = defenseEntityIds[3];

            map["LB-L"] = defenseEntityIds[4];
            map["MLB"] = defenseEntityIds[5];
            map["LB-R"] = defenseEntityIds[6];

            map["CB-L"] = defenseEntityIds[8];
            map["CB-R"] = defenseEntityIds[9];

            map["S-L"] = defenseEntityIds[10];
            map["S-R"] = defenseEntityIds[10];
        }

        return map;
    }
}
