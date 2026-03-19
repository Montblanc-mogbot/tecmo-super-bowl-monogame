using Arch.Core;
using Arch.Core.Extensions;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Updates per-entity SpeedModifier timers.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/SpeedModifierSystem.cs
/// </summary>
public sealed class SpeedModifierSystem
{
    public void Update(World world, float dtSeconds)
    {
        if (dtSeconds <= 0f)
            return;

        var q = new QueryDescription().WithAll<SpeedModifier>();
        world.Query(in q, (Entity e, ref SpeedModifier sm) =>
        {
            if (sm.TimerSeconds <= 0f)
                return;

            sm.TimerSeconds -= dtSeconds;
            if (sm.TimerSeconds <= 0f)
            {
                sm.TimerSeconds = 0f;
                sm.MaxSpeedMultiplier = 1.0f;
            }
            e.Set(sm);
        });
    }
}
