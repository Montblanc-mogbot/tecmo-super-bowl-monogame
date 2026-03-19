using Arch.Core;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Emits AI debug drawables/labels.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/AIDebugSystem.cs
/// </summary>
public sealed class AIDebugSystem
{
    public void Update(World world)
    {
        // TODO: Populate AiDebugDrawable from Behavior/Coverage/Route state.
        // For now, keep deterministic no-op.
        _ = world;
    }
}
