using Arch.Core;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Converts certain tackle whistles into fumble events.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/FumbleOnTackleWhistleSystem.cs
/// </summary>
public sealed class FumbleOnTackleWhistleSystem
{
    public void Update(World world)
    {
        // TODO: port fumble rules + RNG.
        _ = world;
    }
}
