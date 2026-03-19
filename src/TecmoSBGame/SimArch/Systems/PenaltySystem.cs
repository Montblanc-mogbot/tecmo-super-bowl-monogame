using Arch.Core;
using TecmoSBGame.SimArch.State;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Penalty detection/enforcement.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/PenaltySystem.cs
/// </summary>
public sealed class PenaltySystem
{
    public PenaltyRuleset Ruleset { get; set; } = PenaltyRuleset.Off;

    public void Update(World world)
    {
        // TODO: implement deterministic penalty detection.
        _ = world;
    }
}
