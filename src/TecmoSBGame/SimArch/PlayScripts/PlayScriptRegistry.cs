using System;
using System.Collections.Generic;
using System.Linq;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSB;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.PlayScripts;

/// <summary>
/// Holds managed play scripts parsed from YAML for the current Sim instance.
/// Components reference scripts by integer index.
/// </summary>
public sealed class PlayScriptRegistry
{
    private readonly List<PlayScriptOp[]> _scripts = new();

    public int Add(PlayScriptOp[] ops)
    {
        var idx = _scripts.Count;
        _scripts.Add(ops);
        return idx;
    }

    public PlayScriptOp[] Get(int index) => _scripts[index];

    public void Clear() => _scripts.Clear();

    public static PlayScriptOp[] CompileReaction(PlayerReactionScript reaction)
    {
        // Minimal compiler: translate only the subset we currently support.
        var ops = new List<PlayScriptOp>(capacity: reaction.Commands.Count);

        foreach (var c in reaction.Commands)
        {
            var cmd = (c.Cmd ?? string.Empty).Trim();

            if (cmd.Equals("loop", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(new PlayScriptOp(PlayScriptOpKind.Loop));
                continue;
            }

            if (cmd.Equals("wait", StringComparison.OrdinalIgnoreCase))
            {
                // YAML currently uses [start_time, end_time] (frames). We treat end_time as a duration in frames for now.
                var frames = c.Params is { Count: > 1 } ? ParseFloat(c.Params[1]) : 0f;
                ops.Add(new PlayScriptOp(PlayScriptOpKind.WaitSeconds, Seconds: frames / 60f));
                continue;
            }

            if (cmd.Equals("wait_until_snap", StringComparison.OrdinalIgnoreCase))
            {
                // SimArch currently snaps immediately on ApplyPlay; treat as no-op.
                continue;
            }

            if (cmd.Equals("move_by", StringComparison.OrdinalIgnoreCase))
            {
                if (c.Params is not { Count: >= 2 })
                    continue;

                var dx = ParseFloat(c.Params[0]);
                var dy = ParseFloat(c.Params[1]);
                ops.Add(new PlayScriptOp(PlayScriptOpKind.MoveBy, Delta: new Vector2(dx, dy)));
                continue;
            }

            if (cmd.Equals("handoff_to", StringComparison.OrdinalIgnoreCase))
            {
                var slot = c.Params is { Count: > 0 } ? c.Params[0]?.ToString() : null;
                var frames = c.Params is { Count: > 1 } ? ParseFloat(c.Params[1]) : 0f;
                if (!string.IsNullOrWhiteSpace(slot))
                    ops.Add(new PlayScriptOp(PlayScriptOpKind.HandoffToSlotAfterSeconds, Seconds: frames / 60f, Slot: slot.Trim()));
                continue;
            }

            if (cmd.Equals("setMS", StringComparison.OrdinalIgnoreCase))
            {
                var v = c.Params is { Count: > 0 } ? ParseFloat(c.Params[0]) : 0f;
                ops.Add(new PlayScriptOp(PlayScriptOpKind.SetMs, Value: v));
                continue;
            }

            if (cmd.Equals("boostRS", StringComparison.OrdinalIgnoreCase))
            {
                var v = c.Params is { Count: > 0 } ? ParseFloat(c.Params[0]) : 0f;
                ops.Add(new PlayScriptOp(PlayScriptOpKind.BoostRs, Value: v));
                continue;
            }
        }

        if (ops.Count == 0 || ops[^1].Kind != PlayScriptOpKind.Loop)
            ops.Add(new PlayScriptOp(PlayScriptOpKind.Loop));

        return ops.ToArray();
    }

    public void AttachSlotScripts(
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

            var ops = CompileReaction(reaction);
            var index = Add(ops);

            var q = new QueryDescription().WithAll<Role>();
            world.Query(in q, (Entity e, ref Role _) =>
            {
                if (e.Id != entityId)
                    return;

                var ps = new PlayScript { ScriptId = index, Ip = 0, WaitSeconds = 0f, PendingHandoffToEntityId = -1 };
                if (!e.Has<PlayScript>())
                    e.Add(ps);
                else
                    e.Set(ps);
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
