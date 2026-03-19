using Arch.Core;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Man coverage behavior (defenders track assigned receivers).
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/ManCoverageSystem.cs
/// </summary>
public sealed class ManCoverageSystem
{
    public void Update(World world, float dtSeconds)
    {
        // TODO: implement man coverage switching + leverage rules.
        _ = world;
        _ = dtSeconds;
    }
}
