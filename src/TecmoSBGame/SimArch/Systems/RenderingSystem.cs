using Arch.Core;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// ECS-driven render preparation (writes to snapshot, sorts sprites, etc.).
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/RenderingSystem.cs
/// </summary>
public sealed class RenderingSystem
{
    public void Update(World world)
    {
        // NOTE: MainGameArch currently renders directly from SimSnapshot.
        // This system is a parity placeholder for future render refactors.
        _ = world;
    }
}
