using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using TecmoSBGame.Components;

namespace TecmoSBGame.Spawning;

/// <summary>
/// Parses Tecmo-ish YAML formation command strings into a small set of script ops.
///
/// Notes:
/// - The YAML currently contains many opcodes; this parser starts with the subset we need
///   to make kickoff/teams move in recognizable lanes.
/// - Unrecognized tokens are kept as NOPs (preserve determinism and allow incremental expansion).
/// </summary>
public static class FormationScriptParser
{
    public static List<FormationScriptOp> Parse(string? commands)
    {
        var ops = new List<FormationScriptOp>();
        if (string.IsNullOrWhiteSpace(commands))
            return ops;

        // Commands are ';' separated.
        var parts = commands.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var token = p.Trim();
            if (token.Length == 0)
                continue;

            // Common stance tokens: "2pt", "3pt" etc.
            if (token.Equals("2pt", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("3pt", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(FormationScriptOp.Nop(token));
                continue;
            }

            // LoopBack FD / FF / FE etc
            if (token.StartsWith("LoopBack", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(new FormationScriptOp(FormationScriptOpKind.LoopBack, Vector2.Zero, 0f, token));
                continue;
            }

            if (token.Equals("TakeControl", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(new FormationScriptOp(FormationScriptOpKind.TakeControl, Vector2.Zero, 0f, token));
                continue;
            }

            if (token.Equals("ComputerTakeControl", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(new FormationScriptOp(FormationScriptOpKind.ComputerTakeControl, Vector2.Zero, 0f, token));
                continue;
            }

            if (token.StartsWith("SetToBlock", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(new FormationScriptOp(FormationScriptOpKind.SetToBlock, Vector2.Zero, 0f, token));
                continue;
            }

            if (token.Equals("PassBlock", StringComparison.OrdinalIgnoreCase) || token.Contains("PassBlock", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(new FormationScriptOp(FormationScriptOpKind.PassBlock, Vector2.Zero, 0f, token));
                continue;
            }

            // MoveAbsolute / B4-MoveAbsolute(XX YY)
            if (TryParseHexPairArg(token, "MoveAbsolute", out var abs))
            {
                ops.Add(new FormationScriptOp(FormationScriptOpKind.MoveAbsolute, abs, 0f, token));
                continue;
            }

            // MoveRelative / MoveRelative(XX YY)
            if (TryParseHexPairArg(token, "MoveRelative", out var rel))
            {
                // Treat bytes as signed offsets.
                rel = new Vector2(unchecked((sbyte)(byte)rel.X), unchecked((sbyte)(byte)rel.Y));
                ops.Add(new FormationScriptOp(FormationScriptOpKind.MoveRelative, rel, 0f, token));
                continue;
            }

            // Pause / F4-Pause(00) / F5-Pause(01 19)
            if (TryParsePause(token, out var seconds))
            {
                ops.Add(new FormationScriptOp(FormationScriptOpKind.Pause, Vector2.Zero, seconds, token));
                continue;
            }

            // Default: unknown token.
            ops.Add(new FormationScriptOp(FormationScriptOpKind.Unknown, Vector2.Zero, 0f, token));
        }

        return ops;
    }

    private static bool TryParseHexPairArg(string token, string opName, out Vector2 vec)
    {
        vec = Vector2.Zero;

        // Accept forms like:
        // - "MoveAbsolute(78 B0)"
        // - "B4-MoveAbsolute(78 B0)"
        var idx = token.IndexOf(opName, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return false;

        var open = token.IndexOf('(', idx);
        var close = token.IndexOf(')', open + 1);
        if (open < 0 || close < 0 || close <= open)
            return false;

        var inner = token.Substring(open + 1, close - open - 1).Trim();
        var parts = inner.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false;

        if (!byte.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var xb))
            return false;
        if (!byte.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var yb))
            return false;

        vec = new Vector2(xb, yb);
        return true;
    }

    private static bool TryParsePause(string token, out float seconds)
    {
        seconds = 0f;

        var idx = token.IndexOf("Pause", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return false;

        var open = token.IndexOf('(', idx);
        var close = token.IndexOf(')', open + 1);
        if (open < 0 || close < 0 || close <= open)
            return false;

        var inner = token.Substring(open + 1, close - open - 1).Trim();
        if (inner.Length == 0)
            return true;

        // Pause can be one byte or two bytes.
        var parts = inner.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            if (!byte.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b0))
                return false;

            // Interpret as frames.
            seconds = b0 / 60f;
            return true;
        }

        if (!byte.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hi))
            return false;
        if (!byte.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var lo))
            return false;

        var frames = (hi << 8) | lo;
        seconds = frames / 60f;
        return true;
    }
}
