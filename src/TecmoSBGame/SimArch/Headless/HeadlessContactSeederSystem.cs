using Arch.Core;

namespace TecmoSBGame.SimArch.Headless;

/// <summary>
/// Headless-only helper to seed deterministic contact signals.
///
/// Ported from: ArchiveMge/Headless/HeadlessContactSeederSystem.cs
/// </summary>
public sealed class HeadlessContactSeederSystem
{
    public void Update(World world)
    {
        // TODO: Port once SimArch event bus usage is unified with contact systems.
    }
}
