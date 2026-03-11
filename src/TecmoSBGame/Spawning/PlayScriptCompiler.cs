using System;
using System.Collections.Generic;
using TecmoSB;
using TecmoSBGame.Components;

namespace TecmoSBGame.Spawning;

/// <summary>
/// Compiles engine-native PlayData YAML commands into <see cref="PlayScriptOp"/>.
///
/// The YAML commands are named for the engine (not the ROM), but each should map
/// cleanly back to one ROM opcode (documented in docs/rom-command-mapping.md).
/// </summary>
public static class PlayScriptCompiler
{
    public static List<PlayScriptOp> Compile(PlayerReactionScript script)
    {
        var ops = new List<PlayScriptOp>();

        foreach (var c in script.Commands)
        {
            var cmd = (c.Cmd ?? string.Empty).Trim();
            if (cmd.Length == 0)
                continue;

            switch (cmd)
            {
                case "wait_until_snap":
                    // Params: stance (string)
                    var stance = c.Params is { Count: > 0 } ? c.Params[0]?.ToString() : "";
                    ops.Add(new PlayScriptOp(PlayScriptOpKind.WaitUntilSnap, 0, 0, stance, cmd));
                    break;

                case "set_anchor":
                    // Params: kind (string), dx (float/int), dy (float/int)
                    var kind = c.Params is { Count: > 0 } ? c.Params[0]?.ToString() : "";
                    var dx = c.Params is { Count: > 1 } ? ParseFloat(c.Params[1]) : 0f;
                    var dy = c.Params is { Count: > 2 } ? ParseFloat(c.Params[2]) : 0f;
                    ops.Add(new PlayScriptOp(PlayScriptOpKind.SetAnchor, dx, dy, kind, cmd));
                    break;

                case "move_by":
                    // Params: dx, dy
                    ops.Add(new PlayScriptOp(PlayScriptOpKind.MoveBy,
                        c.Params is { Count: > 0 } ? ParseFloat(c.Params[0]) : 0f,
                        c.Params is { Count: > 1 } ? ParseFloat(c.Params[1]) : 0f,
                        null,
                        cmd));
                    break;

                case "move_to_anchor_offset":
                    // Params: dx, dy
                    ops.Add(new PlayScriptOp(PlayScriptOpKind.MoveToAnchorOffset,
                        c.Params is { Count: > 0 } ? ParseFloat(c.Params[0]) : 0f,
                        c.Params is { Count: > 1 } ? ParseFloat(c.Params[1]) : 0f,
                        null,
                        cmd));
                    break;

                case "pass_block":
                    ops.Add(new PlayScriptOp(PlayScriptOpKind.PassBlock, 0, 0, null, cmd));
                    break;

                case "pursue_ballcarrier":
                case "pursue_ball_carrier":
                    ops.Add(new PlayScriptOp(PlayScriptOpKind.PursueBallCarrier, 0, 0, null, cmd));
                    break;

                case "rush_qb":
                case "rushqb":
                    ops.Add(new PlayScriptOp(PlayScriptOpKind.RushQb, 0, 0, null, cmd));
                    break;

                case "handoff_to":
                    // Params: slot (string)
                    var toSlot = c.Params is { Count: > 0 } ? c.Params[0]?.ToString() : "";
                    ops.Add(new PlayScriptOp(PlayScriptOpKind.HandoffTo, 0, 0, toSlot, cmd));
                    break;

                case "pull_and_block":
                    // Params: dx, dy
                    ops.Add(new PlayScriptOp(PlayScriptOpKind.PullAndBlock,
                        c.Params is { Count: > 0 } ? ParseFloat(c.Params[0]) : 0f,
                        c.Params is { Count: > 1 } ? ParseFloat(c.Params[1]) : 0f,
                        null,
                        cmd));
                    break;

                case "loop":
                    ops.Add(new PlayScriptOp(PlayScriptOpKind.Loop, 0, 0, null, cmd));
                    break;

                default:
                    ops.Add(new PlayScriptOp(PlayScriptOpKind.Unknown, 0, 0, null, cmd));
                    break;
            }
        }

        return ops;
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
