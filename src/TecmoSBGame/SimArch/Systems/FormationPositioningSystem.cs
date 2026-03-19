using Arch.Core;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Positions players into their formation pre-snap.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/FormationPositioningSystem.cs
/// </summary>
public sealed class FormationPositioningSystem
{
    public void Update(World world)
    {
        // TODO: Port full positioning logic once formation slots + script parsing are finalized.
        _ = world;
    }
}
