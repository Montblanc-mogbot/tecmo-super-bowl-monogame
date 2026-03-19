using Arch.Core;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Field goal block rush timing.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/FieldGoalBlockRushSystem.cs
/// </summary>
public sealed class FieldGoalBlockRushSystem
{
    public void Update(World world, float dtSeconds)
    {
        if (dtSeconds <= 0f)
            return;

        var q = new QueryDescription().WithAll<FieldGoalBlockRush, Behavior>();
        world.Query(in q, (Entity e, ref FieldGoalBlockRush r, ref Behavior beh) =>
        {
            r.ElapsedFrames += (int)(dtSeconds * 60f);
            if (r.ElapsedFrames < r.DelayFrames)
                return;

            // Once delay elapsed, steer in rush direction.
            beh.State = BehaviorState.MovingToPosition;
        });
    }
}
