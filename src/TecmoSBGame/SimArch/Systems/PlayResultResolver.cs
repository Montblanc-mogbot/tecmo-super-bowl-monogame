using Arch.Core;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Computes yards gained + turnover + scoring flags.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/PlayResultResolver.cs
/// </summary>
public sealed class PlayResultResolver
{
    public void Update(World world)
    {
        // TODO: implement once spotting + ball ownership rules are fully wired.
        _ = world;
    }
}
