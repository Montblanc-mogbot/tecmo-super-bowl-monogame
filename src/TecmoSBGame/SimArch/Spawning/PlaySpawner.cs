using System;
using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Spawning;

/// <summary>
/// Applies play selection to an existing scrimmage roster.
///
/// For the first milestone we only support play_number=10 and we attach minimal PlayScript/Behavior state.
/// Later this will be driven by PlayData YAML.
/// </summary>
public static class PlaySpawner
{
    public static void ApplyPlay(
        World world,
        IReadOnlyList<int> offenseEntityIds,
        IReadOnlyList<int> defenseEntityIds,
        int ballEntityId,
        int playNumber)
    {
        // Find key role ids.
        var qbId = FindRole(world, offenseEntityIds, RoleId.QB);
        var hbId = FindRole(world, offenseEntityIds, RoleId.HB);

        if (qbId == 0 || hbId == 0)
            throw new InvalidOperationException("SimArch play spawner requires QB+HB");

        if (playNumber != 10)
        {
            Console.WriteLine($"[sim-arch] ApplyPlay unsupported play_number={playNumber} (only 10 implemented)");
            return;
        }

        // QB: attach playscript state which will handoff to HB after ~38 frames.
        var qb = new Entity(world, qbId);
        if (!qb.Has<PlayScript>())
            qb.Add(new PlayScript { ScriptId = 10, Ip = 0, WaitSeconds = 38f / 60f, PendingHandoffToEntityId = hbId });
        else
            qb.Set(new PlayScript { ScriptId = 10, Ip = 0, WaitSeconds = 38f / 60f, PendingHandoffToEntityId = hbId });

        // Defense: set tracking behavior toward QB initially (rush) then they can be switched to ballcarrier later.
        foreach (var did in defenseEntityIds)
        {
            var d = new Entity(world, did);
            if (!d.IsAlive() || !d.Has<Behavior>())
                continue;

            var b = d.Get<Behavior>();
            b.State = BehaviorState.TrackingEntity;
            b.TargetEntityId = qbId;
            d.Set(b);
        }

        // Ball is held by QB at snap.
        var ball = new Entity(world, ballEntityId);
        if (ball.IsAlive() && ball.Has<Ball>())
        {
            var b = ball.Get<Ball>();
            b.State = TecmoSBGame.State.BallState.Held;
            b.OwnerEntityId = qbId;
            ball.Set(b);
        }

        Console.WriteLine($"[sim-arch] ApplyPlay play_number=10 qb={qbId} hb={hbId} defenseTracking=qb");
    }

    private static int FindRole(World world, IReadOnlyList<int> entityIds, RoleId role)
    {
        foreach (var id in entityIds)
        {
            var e = new Entity(world, id);
            if (!e.IsAlive() || !e.Has<Role>())
                continue;
            if (e.Get<Role>().Id == role)
                return id;
        }
        return 0;
    }
}
