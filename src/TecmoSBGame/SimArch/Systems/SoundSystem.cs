using Arch.Core;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Triggers sound effects based on events/state.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/SoundSystem.cs
/// </summary>
public sealed class SoundSystem
{
    public void Update(World world)
    {
        // TODO: hook to MonoGame audio backend.
        _ = world;
    }
}
