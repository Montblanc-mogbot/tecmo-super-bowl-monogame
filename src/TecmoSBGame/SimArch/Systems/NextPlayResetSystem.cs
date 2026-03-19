using Arch.Core;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Resets simulation back to PreSnap after a play ends.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/NextPlayResetSystem.cs
/// </summary>
public sealed class NextPlayResetSystem
{
    public void Update(World world)
    {
        // TODO: implement full reset pipeline (loop state + rules).
        _ = world;
    }
}
