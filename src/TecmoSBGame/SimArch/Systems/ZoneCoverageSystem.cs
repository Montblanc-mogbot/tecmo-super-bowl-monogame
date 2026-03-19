using Arch.Core;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Zone coverage behavior.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/ZoneCoverageSystem.cs
///
/// NOTE: SimArch currently uses <see cref="CoverageSystem"/> as the active implementation.
/// This type exists to preserve 1:1 system naming during the port.
/// </summary>
public sealed class ZoneCoverageSystem
{
    private readonly CoverageSystem _impl = new();

    public void Update(World world, float dtSeconds, int ballEntityId, System.Collections.Generic.IReadOnlyList<int> defenseEntityIds)
        => _impl.Update(world, dtSeconds, ballEntityId, defenseEntityIds);
}
