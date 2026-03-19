using Arch.Core;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Top-level rules coordinator (slice/phase driver).
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/GameStateSystem.cs
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/GameStateSystem.Scrimmage.cs
/// </summary>
public sealed class GameStateSystem
{
    public void Update(World world)
    {
        // TODO: orchestrate pre-snap -> in-play -> post-play slices.
        _ = world;
    }
}
