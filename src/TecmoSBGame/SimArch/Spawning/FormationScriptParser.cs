using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Spawning;

/// <summary>
/// Parses Tecmo-ish YAML formation command strings into deterministic FormationScriptOps.
///
/// Goal: never emit fallback NOPs for tokens referenced by formation YAML.
/// Unhandled tokens are classified as <see cref="FormationScriptOpKind.NoOp"/> (recognized but ignored).
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Spawning/FormationScriptParser.cs
/// </summary>
public static class FormationScriptParser
{
    public static IReadOnlyList<FormationScriptOp> Parse(string? commands)
    {
        var ops = new List<FormationScriptOp>();
        if (string.IsNullOrWhiteSpace(commands))
            return ops;

        var parts = commands.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var token = p.Trim();
            if (token.Length == 0)
                continue;

            // Stance tokens: treat as recognized no-ops.
            if (token.Equals("2pt", StringComparison.OrdinalIgnoreCase) || token.Equals("3pt", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(FormationScriptOp.NoOp(token));
                continue;
            }

            if (TryParseSetPos(token, out var kind, out var v))
            {
                ops.Add(new FormationScriptOp(kind, v, 0f, token));
                continue;
            }

            if (token.StartsWith("LoopBack", StringComparison.OrdinalIgnoreCase))
            {
                // LoopBack FD/FE/FF etc. We don't currently use the arg; keep raw.
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

            if (token.Contains("SwitchIcon", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(new FormationScriptOp(FormationScriptOpKind.SwitchIcon, Vector2.Zero, 0f, token));
                continue;
            }

            if (TryParseHexPairArg(token, "MoveAbsolute", out var abs))
            {
                ops.Add(new FormationScriptOp(FormationScriptOpKind.MoveAbsolute, abs, 0f, token));
                continue;
            }

            if (TryParseHexPairArg(token, "MoveRelative", out var rel))
            {
                rel = new Vector2(unchecked((sbyte)(byte)rel.X), unchecked((sbyte)(byte)rel.Y));
                ops.Add(new FormationScriptOp(FormationScriptOpKind.MoveRelative, rel, 0f, token));
                continue;
            }

            if (TryParsePause(token, out var seconds))
            {
                ops.Add(new FormationScriptOp(FormationScriptOpKind.Pause, Vector2.Zero, seconds, token));
                continue;
            }

            // Common opaque opcodes: FC/CD/CF
            if (TryParseHexPairArg(token, "FC", out var fc))
            {
                ops.Add(new FormationScriptOp(FormationScriptOpKind.Fc, fc, 0f, token));
                continue;
            }

            if (TryParseHexPairArg(token, "CD", out var cd))
            {
                cd = new Vector2(unchecked((sbyte)(byte)cd.X), unchecked((sbyte)(byte)cd.Y));
                ops.Add(new FormationScriptOp(FormationScriptOpKind.Cd, cd, 0f, token));
                continue;
            }

            if (TryParseHexPairArg(token, "CF", out var cf))
            {
                cf = new Vector2(unchecked((sbyte)(byte)cf.X), unchecked((sbyte)(byte)cf.Y));
                ops.Add(new FormationScriptOp(FormationScriptOpKind.Cf, cf, 0f, token));
                continue;
            }

            if (token.StartsWith("JumpTo", StringComparison.OrdinalIgnoreCase) || token.Contains("-JumpTo", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(new FormationScriptOp(FormationScriptOpKind.JumpTo, Vector2.Zero, 0f, token));
                continue;
            }

            if (TryParseSingleHexArg(token, "Boost-MS", out var ms))
            {
                ops.Add(new FormationScriptOp(FormationScriptOpKind.BoostMs, new Vector2(ms, 0), 0f, token));
                continue;
            }

            if (TryParseSingleHexArg(token, "Boost-HP", out var hp))
            {
                ops.Add(new FormationScriptOp(FormationScriptOpKind.BoostHp, new Vector2(hp, 0), 0f, token));
                continue;
            }

            if (TryParseSingleHexArg(token, "FaceDirection", out var dir))
            {
                ops.Add(new FormationScriptOp(FormationScriptOpKind.FaceDirection, new Vector2(dir, 0), 0f, token));
                continue;
            }

            if (token.Contains("ShotgunHike", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(new FormationScriptOp(FormationScriptOpKind.ShotgunHike, Vector2.Zero, 0f, token));
                continue;
            }

            if (token.Contains("Punt", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(new FormationScriptOp(FormationScriptOpKind.Punt, Vector2.Zero, 0f, token));
                continue;
            }

            if (token.Contains("FieldGoalTakeSnap", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(new FormationScriptOp(FormationScriptOpKind.FieldGoalTakeSnap, Vector2.Zero, 0f, token));
                continue;
            }

            if (token.Contains("FieldGoal", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(new FormationScriptOp(FormationScriptOpKind.FieldGoal, Vector2.Zero, 0f, token));
                continue;
            }

            // Block-* tokens: recognized but ignored for now.
            if (token.StartsWith("Block-", StringComparison.OrdinalIgnoreCase) || token.Contains("-Block-", StringComparison.OrdinalIgnoreCase))
            {
                ops.Add(FormationScriptOp.NoOp(token));
                continue;
            }

            // Anything else: treat as recognized no-op to avoid parser fallback.
            ops.Add(FormationScriptOp.NoOp(token));
        }

        return ops;
    }

    private static bool TryParseSetPos(string token, out FormationScriptOpKind kind, out Vector2 vec)
    {
        kind = FormationScriptOpKind.NoOp;
        vec = Vector2.Zero;

        if (TryParseHexPairArg(token, "SetPosFromKick", out vec))
        {
            kind = FormationScriptOpKind.SetPosFromKick;
            return true;
        }
        if (TryParseHexPairArg(token, "SetPosFromHike", out vec))
        {
            kind = FormationScriptOpKind.SetPosFromHike;
            return true;
        }
        if (TryParseHexPairArg(token, "SetPosFromMid", out vec))
        {
            kind = FormationScriptOpKind.SetPosFromMid;
            return true;
        }

        return false;
    }

    private static bool TryParseHexPairArg(string token, string opName, out Vector2 vec)
    {
        vec = Vector2.Zero;

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

    private static bool TryParseSingleHexArg(string token, string opName, out byte value)
    {
        value = 0;

        var idx = token.IndexOf(opName, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return false;

        var open = token.IndexOf('(', idx);
        var close = token.IndexOf(')', open + 1);
        if (open < 0 || close < 0 || close <= open)
            return false;

        var inner = token.Substring(open + 1, close - open - 1).Trim();
        if (inner.Length == 0)
            return true;

        var parts = inner.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 1)
            return false;

        return byte.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
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

        var parts = inner.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            if (!byte.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b0))
                return false;
            seconds = b0 / 60f;
            return true;
        }

        if (parts.Length >= 2)
        {
            if (!byte.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hi))
                return false;
            if (!byte.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var lo))
                return false;

            var frames = (hi << 8) | lo;
            seconds = frames / 60f;
            return true;
        }

        return false;
    }
}
