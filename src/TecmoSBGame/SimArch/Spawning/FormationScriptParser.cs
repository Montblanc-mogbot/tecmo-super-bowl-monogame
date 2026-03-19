using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Spawning;

/// <summary>
/// Parses formation script command strings (YAML) into FormationScriptOps.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Spawning/FormationScriptParser.cs
/// </summary>
public static class FormationScriptParser
{
    public static IReadOnlyList<FormationScriptOp> Parse(IReadOnlyList<string> commands)
    {
        if (commands is null) throw new ArgumentNullException(nameof(commands));

        // TODO: Full command grammar port. For now, preserve determinism by emitting Nops.
        var ops = new List<FormationScriptOp>(commands.Count);
        foreach (var raw in commands)
            ops.Add(FormationScriptOp.Nop(raw ?? string.Empty));

        if (ops.Count == 0)
            ops.Add(FormationScriptOp.Nop("(empty)"));

        return ops;
    }

    // Helper for future ports
    private static Vector2 ParseVec(string a, string b)
        => new(float.TryParse(a, out var x) ? x : 0f, float.TryParse(b, out var y) ? y : 0f);
}
