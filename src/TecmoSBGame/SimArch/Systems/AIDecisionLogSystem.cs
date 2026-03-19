using Arch.Core;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Optional headless-friendly AI decision logging.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/AIDecisionLogSystem.cs
/// </summary>
public sealed class AIDecisionLogSystem
{
    public void Update(World world)
    {
        // TODO: Log QB reads, coverage switches, rush move attempts, etc.
        _ = world;
    }
}
