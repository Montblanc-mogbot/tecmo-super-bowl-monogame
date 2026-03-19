using System;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.Routes;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Frame-timed route following (SimArch).
///
/// This system sets Behavior.TargetPosition to the current route node target.
/// MovementSystem then performs turn-limited steering toward that target.
/// </summary>
public sealed class RouteFollowSystem
{
    public void Update(World world, float dtSeconds, RouteRegistry routes)
    {
        if (dtSeconds <= 0f)
            return;

        var frames = Math.Max(1, (int)MathF.Round(dtSeconds * 60f));

        var q = new QueryDescription().WithAll<RouteFollow, Position, Behavior>();
        world.Query(in q, (Entity e, ref RouteFollow rf, ref Position pos, ref Behavior beh) =>
        {
            if (rf.Completed)
                return;

            if (!rf.HasAnchor)
            {
                rf.AnchorPosition = pos.Value;
                rf.HasAnchor = true;
            }

            var plan = routes.Get(rf.RouteId);
            if (rf.NodeIndex < 0 || rf.NodeIndex >= plan.Nodes.Length)
            {
                rf.Completed = true;
                return;
            }

            // Initialize node timer if needed.
            if (rf.FramesRemainingInNode <= 0)
                rf.FramesRemainingInNode = Math.Max(1, plan.Nodes[rf.NodeIndex].Frames);

            // Determine absolute target for this node: anchor + cumulative deltas up through this node.
            var target = rf.AnchorPosition;
            for (var i = 0; i <= rf.NodeIndex && i < plan.Nodes.Length; i++)
                target += plan.Nodes[i].Delta;

            beh.State = BehaviorState.MovingToPosition;
            beh.TargetEntityId = -1;
            beh.TargetPosition = target;

            rf.FramesRemainingInNode -= frames;
            if (rf.FramesRemainingInNode > 0)
                return;

            // Advance to next node.
            rf.NodeIndex++;
            rf.FramesRemainingInNode = 0;

            if (rf.NodeIndex >= plan.Nodes.Length)
                rf.Completed = true;
        });
    }
}
