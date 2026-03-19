using System;
using System.Collections.Generic;
using System.Linq;
using Arch.Core;
using Arch.Core.Extensions;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Spawning;

// Ported from: src/TecmoSBGame/ArchiveMge/Spawning/PlaySpawner.cs

/// <summary>
/// Applies play selection to an existing scrimmage roster.
///
/// Current scope (Arch sim):
/// - Read PlayData YAML PlayDefinition for the selected play_number.
/// - If the QB reaction contains a <c>handoff_to</c> command, schedule a delayed handoff.
/// - Default defense behavior: track/rush QB (until richer defensive scripts are implemented).
/// </summary>
public static partial class PlaySpawner
{
    public static void ApplyPlay(
        World world,
        TecmoSB.PlayDataConfig playData,
        IReadOnlyList<int> offenseEntityIds,
        IReadOnlyList<int> defenseEntityIds,
        int ballEntityId,
        int playNumber,
        SimArch.PlayScripts.PlayScriptRegistry scriptRegistry,
        SimArch.Routes.RouteRegistry routeRegistry)
    {
        if (world is null) throw new ArgumentNullException(nameof(world));
        if (playData is null) throw new ArgumentNullException(nameof(playData));
        if (routeRegistry is null) throw new ArgumentNullException(nameof(routeRegistry));

        var def = playData.Plays.FirstOrDefault(p => p.PlayNumber == playNumber);
        if (def is null)
        {
            Console.WriteLine($"[sim-arch] ApplyPlay missing play_number={playNumber} in PlayData YAML");
            return;
        }

        // Find key role ids.
        var qbId = FindRole(world, offenseEntityIds, RoleId.QB);
        var hbId = FindRole(world, offenseEntityIds, RoleId.HB);

        // Build slot -> entityId lookup for this roster.
        var slotToEntityId = BuildSlotLookup(world, offenseEntityIds);
        var defSlotToEntityId = BuildDefensiveSlotLookup(world, defenseEntityIds);

        if (qbId < 0)
            throw new InvalidOperationException("SimArch play spawner requires QB");

        // Default: ball held by QB at snap.
        SetBallOwner(world, ballEntityId, qbId);

        // Attach per-slot scripts/routes from YAML.
        scriptRegistry.AttachSlotScripts(world, playData, def.Offense, slotToEntityId);
        scriptRegistry.AttachSlotScripts(world, playData, def.Defense, defSlotToEntityId);

        AttachRoutes(world, routeRegistry, playData, def.Offense, slotToEntityId);

        // NOTE: handoff_to is handled by the generic SimArch playscript runner (compiled into the registry).

        // QB AI (dropback/read/progression scaffold):
        // - If the QB reaction contains a handoff_to, treat it as a run play and disable QB pass AI.
        // - Otherwise, enable QB pass AI so we can test pass flight in SimArch.
        var qbReactionId = def.Offense.TryGetValue("QB", out var qbRid) ? qbRid : null;
        var qbIsRunPlay = qbReactionId is not null && ReactionContainsHandoffTo(playData, qbReactionId);
        SetOrAddQbBrain(world, qbId, enabled: !qbIsRunPlay);

        // Defense default fallback: if a defender has no movement intent, track QB.
        var defenseSet = new HashSet<int>(defenseEntityIds);
        var defQuery = new QueryDescription().WithAll<Behavior>();
        world.Query(in defQuery, (Entity e, ref Behavior b) =>
        {
            if (!defenseSet.Contains(e.Id))
                return;

            if (b.State == BehaviorState.Idle)
            {
                b.State = BehaviorState.TrackingEntity;
                b.TargetEntityId = qbId;
            }
        });

        Console.WriteLine($"[sim-arch] ApplyPlay play_number={playNumber} qb={qbId} hb={hbId} yaml={def.Description}");
    }

    private static void SetBallOwner(World world, int ballEntityId, int ownerEntityId)
    {
        var q = new QueryDescription().WithAll<Ball>();
        world.Query(in q, (Entity e, ref Ball b) =>
        {
            if (e.Id != ballEntityId)
                return;

            b.State = Components.BallState.Held;
            b.OwnerEntityId = ownerEntityId;
        });
    }

    private static void SetOrAddQbBrain(World world, int qbEntityId, bool enabled)
    {
        var q = new QueryDescription().WithAll<Role>();
        world.Query(in q, (Entity e, ref Role _) =>
        {
            if (e.Id != qbEntityId)
                return;

            if (!enabled)
            {
                if (e.Has<QbBrain>())
                    e.Remove<QbBrain>();
                return;
            }

            var brain = new QbBrain
            {
                DropbackFramesRemaining = 30,
                ReadIndex = 0,
                PassRequested = false,
                PassType = TecmoSBGame.SimArch.PassType.Bullet,
            };

            if (!e.Has<QbBrain>())
                e.Add(brain);
            else
                e.Set(brain);
        });
    }

    private static bool ReactionContainsHandoffTo(TecmoSB.PlayDataConfig playData, string reactionId)
    {
        var reaction = playData.PlayerReactions.FirstOrDefault(r => string.Equals(r.Id, reactionId, StringComparison.OrdinalIgnoreCase));
        if (reaction is null)
            return false;

        foreach (var c in reaction.Commands)
        {
            var cmd = (c.Cmd ?? string.Empty).Trim();
            if (cmd.Equals("handoff_to", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
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
