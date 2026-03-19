using System;
using TecmoSB;
using TecmoSBGame.SimArch.PlayScripts;

namespace TecmoSBGame.SimArch.Spawning;

/// <summary>
/// High-level helper that finds scripts in PlayData and compiles them to ops.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Spawning/PlayDataScriptCompiler.cs
/// </summary>
public static class PlayDataScriptCompiler
{
    public static PlayScriptOp[] CompileById(PlayDataConfig playData, string id)
    {
        if (playData is null) throw new ArgumentNullException(nameof(playData));
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("script id required", nameof(id));

        PlayerReactionScript? reaction = null;
        foreach (var r in playData.PlayerReactions)
        {
            if (string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                reaction = r;
                break;
            }
        }

        if (reaction is null)
            return [ new PlayScriptOp(PlayScriptOpKind.Loop) ];

        return PlayScriptRegistry.CompileReaction(reaction);
    }
}
