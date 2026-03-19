using Arch.Core;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Chooses which player is currently user-controlled.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/PlayerControlSystem.cs
/// </summary>
public sealed class PlayerControlSystem
{
    public void Update(World world)
    {
        // TODO: implement selection rules (nearest defender, ballcarrier, etc.).
        _ = world;
    }
}
