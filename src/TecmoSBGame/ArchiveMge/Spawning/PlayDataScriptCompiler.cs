using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TecmoSB;
using TecmoSBGame.Components;

namespace TecmoSBGame.Spawning;

/// <summary>
/// Compiles ROM-style play command scripts (loaded from PlayData YAML) into a small,
/// deterministic set of FormationScriptOps that our ECS can execute today.
///
/// This is intentionally incremental: we only compile commands we can model in BehaviorComponent.
/// Unrecognized commands become Unknown/Nop so we can extend without breaking determinism.
/// </summary>
public static class PlayDataScriptCompiler
{
    public static List<FormationScriptOp> Compile(PlayerReactionScript script)
    {
        var ops = new List<FormationScriptOp>();
        if (script.Commands is null)
            return ops;

        foreach (var c in script.Commands)
        {
            var cmd = (c.Cmd ?? string.Empty).Trim();
            if (cmd.Length == 0)
                continue;

            switch (cmd)
            {
                case "waitForSnap2PointStance":
                case "waitForSnap3PointStance":
                    ops.Add(new FormationScriptOp(FormationScriptOpKind.WaitForSnap, Vector2.Zero, 0f, cmd));
                    break;

                case "wait":
                    ops.Add(new FormationScriptOp(FormationScriptOpKind.Pause, Vector2.Zero, ParseWaitSeconds(c.Params), cmd));
                    break;

                case "moveRelative":
                case "pullRelative":
                    ops.Add(new FormationScriptOp(FormationScriptOpKind.MoveRelative, ParseSignedBytePair(c.Params), 0f, cmd));
                    break;

                case "moveDuringKickoff":
                    ops.Add(new FormationScriptOp(FormationScriptOpKind.MoveRelative, ParseSignedBytePair(c.Params), 0f, cmd));
                    break;

                case "loopTo":
                    // We don't model labels/addresses yet; loop back to script start.
                    ops.Add(new FormationScriptOp(FormationScriptOpKind.LoopBack, Vector2.Zero, 0f, cmd));
                    break;

                case "playerTakesControl":
                    ops.Add(new FormationScriptOp(FormationScriptOpKind.TakeControl, Vector2.Zero, 0f, cmd));
                    break;

                case "computerTakesControl":
                    ops.Add(new FormationScriptOp(FormationScriptOpKind.ComputerTakeControl, Vector2.Zero, 0f, cmd));
                    break;

                case "setToBlock":
                case "block":
                case "setToGrapple":
                case "boostRS":
                case "setMS":
                case "turn":
                case "setPositionBallPlacement":
                case "setPositionMiddleOfField":
                case "setPositionFromKickoffB0":
                case "setPositionFromKickoffB1":
                case "setPositionFromKickoffB0B1":
                case "punt":
                case "changePlayerIconToReturner":
                    ops.Add(FormationScriptOp.Nop(cmd));
                    break;

                default:
                    ops.Add(new FormationScriptOp(FormationScriptOpKind.Unknown, Vector2.Zero, 0f, cmd));
                    break;
            }
        }

        return ops;
    }

    private static float ParseWaitSeconds(IReadOnlyList<object>? args)
    {
        // ROM wait often uses [hi, lo] frames. We accept:
        // - [0x00, 0x10] => 16 frames
        // - [0x10] => 16 frames
        if (args is null || args.Count == 0)
            return 0f;

        int frames;
        if (args.Count == 1)
            frames = ParseByte(args[0]);
        else
            frames = (ParseByte(args[0]) << 8) | ParseByte(args[1]);

        return Math.Max(0, frames) / 60f;
    }

    private static Vector2 ParseSignedBytePair(IReadOnlyList<object>? args)
    {
        if (args is null || args.Count < 2)
            return Vector2.Zero;

        var x = unchecked((sbyte)(byte)ParseByte(args[0]));
        var y = unchecked((sbyte)(byte)ParseByte(args[1]));
        return new Vector2(x, y);
    }

    private static int ParseByte(object o)
    {
        // YamlDotNet parses 0xNN as int.
        return o switch
        {
            byte b => b,
            sbyte sb => (byte)sb,
            short s => (byte)s,
            int i => (byte)i,
            long l => (byte)l,
            string str => str.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToInt32(str, 16)
                : int.TryParse(str, out var n) ? n : 0,
            _ => 0,
        };
    }
}
