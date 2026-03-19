using System;
using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Spawning;

public static partial class PlaySpawner
{
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
    // In SimArch, defenders are tagged with PlayerRole.Slot during formation spawn.
    private static IReadOnlyDictionary<string, int> BuildDefensiveSlotLookup(World world, IReadOnlyList<int> defenseEntityIds)
    {
        var allow = new HashSet<int>(defenseEntityIds);
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var q = new QueryDescription().WithAll<PlayerRole>();
        world.Query(in q, (Entity e, ref PlayerRole pr) =>
        {
            if (!allow.Contains(e.Id))
                return;

            var slot = (pr.Slot ?? string.Empty).Trim();
            if (slot.Length == 0)
                return;

            if (!map.ContainsKey(slot))
                map[slot] = e.Id;
        });

        return map;
    }
}
