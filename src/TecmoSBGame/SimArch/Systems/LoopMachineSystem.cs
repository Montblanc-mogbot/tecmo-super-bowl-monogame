using Arch.Core;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Loop machine driver (game loop + on-field loop).
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/LoopMachineSystem.cs
/// </summary>
public sealed class LoopMachineSystem
{
    public void Update(World world)
    {
        // TODO: wire LoopState + YAML machines.
        _ = world;
    }
}
