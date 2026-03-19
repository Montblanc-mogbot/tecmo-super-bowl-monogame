using System;
using System.Collections.Generic;
using System.Linq;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSB;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.Routes;

namespace TecmoSBGame.SimArch.Spawning;

public static partial class PlaySpawner
{
    private static void AttachRoutes(
        World world,
        RouteRegistry routes,
        PlayDataConfig playData,
        IReadOnlyDictionary<string, string>? offenseSlotToReactionId,
        IReadOnlyDictionary<string, int> offenseSlotToEntityId)
    {
        if (offenseSlotToReactionId is null)
            return;

        foreach (var (slot, reactionId) in offenseSlotToReactionId)
        {
            if (!offenseSlotToEntityId.TryGetValue(slot, out var entityId))
                continue;

            // Only attach routes for eligible receiver-like roles.
            var isEligible = slot.Equals("WR1", StringComparison.OrdinalIgnoreCase)
                             || slot.Equals("WR2", StringComparison.OrdinalIgnoreCase)
                             || slot.Equals("TE", StringComparison.OrdinalIgnoreCase)
                             || slot.Equals("HB", StringComparison.OrdinalIgnoreCase)
                             || slot.Equals("FB", StringComparison.OrdinalIgnoreCase);

            if (!isEligible)
                continue;

            var reaction = playData.PlayerReactions.FirstOrDefault(r => string.Equals(r.Id, reactionId, StringComparison.OrdinalIgnoreCase));
            if (reaction is null)
                continue;

            // Heuristic: treat sequences of move_by commands as route nodes.
            var nodes = new List<RouteNode>();
            foreach (var c in reaction.Commands)
            {
                var cmd = (c.Cmd ?? string.Empty).Trim();
                if (!cmd.Equals("move_by", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (c.Params is not { Count: >= 2 })
                    continue;

                var dx = ParseFloat(c.Params[0]);
                var dy = ParseFloat(c.Params[1]);

                // Default node duration: 18 frames (~0.30s). Tunable later.
                nodes.Add(new RouteNode(new Vector2(dx, dy), Frames: 18));
            }

            if (nodes.Count == 0)
                continue;

            var routeId = routes.Add(new RoutePlan { Nodes = nodes.ToArray() });

            var q = new QueryDescription().WithAll<Role>();
            world.Query(in q, (Entity e, ref Role _) =>
            {
                if (e.Id != entityId)
                    return;

                var rf = new RouteFollow
                {
                    RouteId = routeId,
                    NodeIndex = 0,
                    FramesRemainingInNode = 0,
                    HasAnchor = false,
                    AnchorPosition = Vector2.Zero,
                    Completed = false,
                };

                if (!e.Has<RouteFollow>())
                    e.Add(rf);
                else
                    e.Set(rf);
            });
        }
    }

    private static float ParseFloat(object? o)
    {
        return o switch
        {
            null => 0f,
            float f => f,
            double d => (float)d,
            int i => i,
            long l => l,
            string s => float.TryParse(s, out var n) ? n : 0f,
            _ => 0f,
        };
    }
}
