using Arch.Core;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Executes FormationScript ops and converts them into Behavior targets.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/FormationScriptSystem.cs
/// </summary>
public sealed class FormationScriptSystem
{
    public void Update(World world, float dtSeconds)
    {
        // TODO: Implement interpreter. For now, deterministic no-op.
        _ = dtSeconds;

        var q = new QueryDescription().WithAll<FormationScript>();
        world.Query(in q, (Entity _, ref FormationScript __) =>
        {
        });
    }
}
