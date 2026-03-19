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

        if (qbId < 0 || hbId < 0)
            throw new InvalidOperationException("SimArch play spawner requires QB+HB");

        if (playNumber != 10)
        {
            Console.WriteLine($"[sim-arch] ApplyPlay unsupported play_number={playNumber} (only 10 implemented)");
            return;
        }

        // QB: attach playscript state which will handoff to HB after ~38 frames.
        var qbScript = new PlayScript { ScriptId = 10, Ip = 0, WaitSeconds = 38f / 60f, PendingHandoffToEntityId = hbId };
        var qbQuery = new QueryDescription().WithAll<Role>();
        world.Query(in qbQuery, (Entity e, ref Role r) =>
        {
            if (e.Id != qbId)
                return;

            if (!e.Has<PlayScript>())
                e.Add(qbScript);
            else
                e.Set(qbScript);
        });

        // Defense: set tracking behavior toward QB initially (rush) then they can be switched to ballcarrier later.
        var defenseSet = new HashSet<int>(defenseEntityIds);
        var defQuery = new QueryDescription().WithAll<Behavior>();
        world.Query(in defQuery, (Entity e, ref Behavior b) =>
        {
            if (!defenseSet.Contains(e.Id))
                return;

            b.State = BehaviorState.TrackingEntity;
            b.TargetEntityId = qbId;
        });

        // Ball is held by QB at snap.
        var ballQuery = new QueryDescription().WithAll<Ball>();
        world.Query(in ballQuery, (Entity e, ref Ball b) =>
        {
            if (e.Id != ballEntityId)
                return;

            b.State = TecmoSBGame.SimArch.Components.BallState.Held;
            b.OwnerEntityId = qbId;
        });

        Console.WriteLine($"[sim-arch] ApplyPlay play_number=10 qb={qbId} hb={hbId} defenseTracking=qb");
    }

    private static int FindRole(World world, IReadOnlyList<int> entityIds, RoleId role)
    {
        var allow = new HashSet<int>(entityIds);
        var found = -1;

        var q = new QueryDescription().WithAll<Role>();
        world.Query(in q, (Entity e, ref Role r) =>
        {
            if (found != -1)
                return;
            if (!allow.Contains(e.Id))
                return;
            if (r.Id == role)
                found = e.Id;
        });

        return found;
    }
}
