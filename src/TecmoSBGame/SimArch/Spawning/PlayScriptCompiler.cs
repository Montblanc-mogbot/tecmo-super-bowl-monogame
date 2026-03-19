using System;
using TecmoSB;
using TecmoSBGame.SimArch.PlayScripts;

namespace TecmoSBGame.SimArch.Spawning;

/// <summary>
/// Compiles PlayData YAML reaction scripts into executable ops.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Spawning/PlayScriptCompiler.cs
/// </summary>
public static class PlayScriptCompiler
{
    public static PlayScriptOp[] Compile(PlayerReactionScript reaction)
    {
        if (reaction is null) throw new ArgumentNullException(nameof(reaction));
        return PlayScriptRegistry.CompileReaction(reaction);
    }
}
