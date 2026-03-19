using System;
using System.Collections.Generic;
using System.Linq;
using Arch.Core;
using Arch.Core.Extensions;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Spawning;

/// <summary>
/// Applies play selection to an existing scrimmage roster.
///
/// Current scope (Arch sim):
/// - Read PlayData YAML PlayDefinition for the selected play_number.
/// - If the QB reaction contains a <c>handoff_to</c> command, schedule a delayed handoff.
/// - Default defense behavior: track/rush QB (until richer defensive scripts are implemented).
/// </summary>
public static class PlaySpawner
{
    public static void ApplyPlay(
        World world,
        TecmoSB.PlayDataConfig playData,
        IReadOnlyList<int> offenseEntityIds,
        IReadOnlyList<int> defenseEntityIds,
        int ballEntityId,
        int playNumber)
    {
        if (world is null) throw new ArgumentNullException(nameof(world));
        if (playData is null) throw new ArgumentNullException(nameof(playData));

        var def = playData.Plays.FirstOrDefault(p => p.PlayNumber == playNumber);
        if (def is null)
        {
            Console.WriteLine($"[sim-arch] ApplyPlay missing play_number={playNumber} in PlayData YAML");
            return;
        }

        // Find key role ids.
        var qbId = FindRole(world, offenseEntityIds, RoleId.QB);
        var hbId = FindRole(world, offenseEntityIds, RoleId.HB);

        if (qbId < 0)
            throw new InvalidOperationException("SimArch play spawner requires QB");

        // Default: ball held by QB at snap.
        SetBallOwner(world, ballEntityId, qbId);

        // QB playscript: detect a handoff_to slot+delayFrames from YAML.
        var qbScriptId = def.Offense.TryGetValue("QB", out var qbReactionId) ? qbReactionId : null;
        var qbReaction = qbScriptId is not null
            ? playData.PlayerReactions.FirstOrDefault(r => r.Id == qbScriptId)
            : null;

        if (qbReaction is not null && TryGetHandoff(qbReaction, out var handoffSlot, out var delayFrames))
        {
            var toEntityId = MapSlotToEntityId(world, offenseEntityIds, handoffSlot);
            if (toEntityId < 0)
            {
                Console.WriteLine($"[sim-arch] WARN: handoff_to slot '{handoffSlot}' not found in offense roster");
            }
            else
            {
                var qbScript = new PlayScript
                {
                    ScriptId = playNumber,
                    Ip = 0,
                    WaitSeconds = delayFrames / 60f,
                    PendingHandoffToEntityId = toEntityId,
                };

                SetOrAddPlayScript(world, qbId, qbScript);
            }
        }
        else
        {
            // Ensure QB has no stale handoff from prior play selection.
            SetOrAddPlayScript(world, qbId, new PlayScript { ScriptId = playNumber, Ip = 0, WaitSeconds = 0f, PendingHandoffToEntityId = -1 });
        }

        // Defense: set tracking behavior toward QB initially.
        var defenseSet = new HashSet<int>(defenseEntityIds);
        var defQuery = new QueryDescription().WithAll<Behavior>();
        world.Query(in defQuery, (Entity e, ref Behavior b) =>
        {
            if (!defenseSet.Contains(e.Id))
                return;

            b.State = BehaviorState.TrackingEntity;
            b.TargetEntityId = qbId;
        });

        Console.WriteLine($"[sim-arch] ApplyPlay play_number={playNumber} qb={qbId} hb={hbId} yaml={def.Description}");
    }

    private static void SetOrAddPlayScript(World world, int entityId, PlayScript script)
    {
        var q = new QueryDescription().WithAll<Role>();
        world.Query(in q, (Entity e, ref Role r) =>
        {
            if (e.Id != entityId)
                return;

            if (!e.Has<PlayScript>())
                e.Add(script);
            else
                e.Set(script);
        });
    }

    private static void SetBallOwner(World world, int ballEntityId, int ownerEntityId)
    {
        var q = new QueryDescription().WithAll<Ball>();
        world.Query(in q, (Entity e, ref Ball b) =>
        {
            if (e.Id != ballEntityId)
                return;

            b.State = Components.BallState.Held;
            b.OwnerEntityId = ownerEntityId;
        });
    }

    private static bool TryGetHandoff(TecmoSB.PlayerReactionScript qbReaction, out string slot, out float delayFrames)
    {
        slot = string.Empty;
        delayFrames = 0f;

        foreach (var c in qbReaction.Commands)
        {
            var cmd = (c.Cmd ?? string.Empty).Trim();
            if (!cmd.Equals("handoff_to", StringComparison.OrdinalIgnoreCase))
                continue;

            var toSlot = c.Params is { Count: > 0 } ? c.Params[0]?.ToString() : null;
            if (string.IsNullOrWhiteSpace(toSlot))
                continue;

            slot = toSlot.Trim();
            delayFrames = c.Params is { Count: > 1 } ? ParseFloat(c.Params[1]) : 0f;
            return true;
        }

        return false;
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

    private static int MapSlotToEntityId(World world, IReadOnlyList<int> offenseEntityIds, string slot)
    {
        var wantRole = slot.Trim().ToUpperInvariant() switch
        {
            "QB" => RoleId.QB,
            "HB" => RoleId.HB,
            "FB" => RoleId.FB,
            "WR1" => RoleId.WR1,
            "WR2" => RoleId.WR2,
            "TE" => RoleId.TE,
            "OC" => RoleId.OC,
            "LG" => RoleId.LG,
            "RG" => RoleId.RG,
            "LT" => RoleId.LT,
            "RT" => RoleId.RT,
            _ => RoleId.Unknown,
        };

        if (wantRole == RoleId.Unknown)
            return -1;

        return FindRole(world, offenseEntityIds, wantRole);
    }

    private static int FindRole(World world, IReadOnlyList<int> entityIds, RoleId role)
    {
        var allow = new HashSet<int>(entityIds);
        var found = -1;

        var q = new QueryDescription().WithAll<Role>();
        world.Query(in q, (Entity e, ref Role r) =>
        {
            if (found != -1)
                return;
            if (!allow.Contains(e.Id))
                return;
            if (r.Id == role)
                found = e.Id;
        });

        return found;
    }
}
