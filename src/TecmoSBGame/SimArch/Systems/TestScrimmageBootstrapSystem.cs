using Arch.Core;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Debug/test bootstrap for a scrimmage slice.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/TestScrimmageBootstrapSystem.cs
/// </summary>
public sealed class TestScrimmageBootstrapSystem
{
    public void Update(World world)
    {
        // TODO: spawn a deterministic scrimmage setup for debug.
        _ = world;
    }
}
