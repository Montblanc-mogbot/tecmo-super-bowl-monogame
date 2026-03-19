using System;
using Arch.Core;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Camera follow logic.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/CameraSystem.cs
/// </summary>
public sealed class CameraSystem
{
    public void Update(World world, ref Vector2 cameraTopLeft)
    {
        // Pick the highest-priority target.
        var bestId = -1;
        var bestPri = int.MinValue;
        var bestPos = Vector2.Zero;

        var q = new QueryDescription().WithAll<CameraTarget, Position>();
        world.Query(in q, (Entity e, ref CameraTarget t, ref Position p) =>
        {
            if (t.Priority > bestPri)
            {
                bestPri = t.Priority;
                bestId = e.Id;
                bestPos = p.Value;
            }
        });

        if (bestId < 0)
            return;

        // Simple follow: keep target near center.
        cameraTopLeft = bestPos - new Vector2(128, 112);

        // Clamp later once field bounds are defined.
    }
}
