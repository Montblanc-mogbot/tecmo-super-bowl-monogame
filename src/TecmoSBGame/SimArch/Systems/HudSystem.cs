using Arch.Core;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Updates HUD entities from MatchState/PlayState.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/HudSystem.cs
/// </summary>
public sealed class HudSystem
{
    public void Update(World world)
    {
        // TODO: once UI entities exist in Arch runtime.
        _ = world;
    }
}
