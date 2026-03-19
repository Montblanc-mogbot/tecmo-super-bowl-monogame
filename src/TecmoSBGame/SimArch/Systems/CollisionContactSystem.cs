using System;
using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.Events;

namespace TecmoSBGame.SimArch.Systems;

// Ported from: src/TecmoSBGame/ArchiveMge/Systems/CollisionContactSystem.cs

/// <summary>
/// Discrete, distance-based collision/contact checks (SimArch).
///
/// Layered phases (single system for now):
/// 1) Proximity detection: gather candidate pairs within a radius.
/// 2) Tackle eligibility: defender vs ball-carrier contact candidates.
/// 3) Block engagement: offense (blocker) vs defense contact candidates.
///
/// This system emits low-level contact events only; downstream systems decide consequences.
/// </summary>
public sealed class CollisionContactSystem
{
    // Radii/constants (NES pixel-ish units).
    public float ProximityRadius = 12f;

    public float TackleContactRadiusBase = 8f;
    public float BlockContactRadius = 12f;

    // TODO: incorporate tackle-attempt intent (bonus radius) once input/AI emits an attempt event.

    private readonly struct Pair
    {
        public readonly int A;
        public readonly int B;
        public readonly float DistSq;
        public readonly Vector2 ContactPosition;

        public Pair(int a, int b, float distSq, Vector2 contactPosition)
        {
            A = a;
            B = b;
            DistSq = distSq;
            ContactPosition = contactPosition;
        }
    }

    public void Update(World world, IReadOnlyList<int> offenseEntityIds, IReadOnlyList<int> defenseEntityIds)
    {
        if (world is null) throw new ArgumentNullException(nameof(world));

        // Deterministic iteration order.
        var entities = new List<int>(offenseEntityIds.Count + defenseEntityIds.Count);
        entities.AddRange(offenseEntityIds);
        entities.AddRange(defenseEntityIds);
        entities.Sort();

        // Find current ball owner (if any).
        var ballOwnerId = FindBallOwner(world);

        // Phase 1: proximity candidate pairs.
        var pairs = GatherProximityPairs(world, entities, ProximityRadius);

        // Phase 2: tackle eligibility/contact.
        if (ballOwnerId >= 0)
            EmitTackleContacts(world, pairs, ballOwnerId);

        // Phase 3: block engagement/contact.
        EmitBlockContacts(world, pairs, ballOwnerId);
    }

    private static int FindBallOwner(World world)
    {
        var owner = -1;
        var q = new QueryDescription().WithAll<Ball>();
        world.Query(in q, (Entity e, ref Ball b) =>
        {
            if (owner != -1)
                return;

            if (b.State == Components.BallState.Held)
                owner = b.OwnerEntityId;
        });

        return owner;
    }

    private static List<Pair> GatherProximityPairs(World world, List<int> entities, float radius)
    {
        var radiusSq = radius * radius;
        var pairs = new List<Pair>(capacity: Math.Max(4, entities.Count));

        // Build position lookup once.
        var posById = new Dictionary<int, Vector2>(capacity: entities.Count);
        var qPos = new QueryDescription().WithAll<Position>();
        world.Query(in qPos, (Entity e, ref Position p) =>
        {
            posById[e.Id] = p.Value;
        });

        for (var i = 0; i < entities.Count; i++)
        {
            var a = entities[i];
            if (!posById.TryGetValue(a, out var posA))
                continue;

            for (var j = i + 1; j < entities.Count; j++)
            {
                var b = entities[j];
                if (!posById.TryGetValue(b, out var posB))
                    continue;

                var dx = posB.X - posA.X;
                var dy = posB.Y - posA.Y;
                var distSq = (dx * dx) + (dy * dy);

                if (distSq > radiusSq)
                    continue;

                var contactPos = (posA + posB) * 0.5f;
                pairs.Add(new Pair(a, b, distSq, contactPos));
            }
        }

        return pairs;
    }

    private void EmitTackleContacts(World world, List<Pair> pairs, int ballCarrierId)
    {
        var radiusSq = TackleContactRadiusBase * TackleContactRadiusBase;

        for (var i = 0; i < pairs.Count; i++)
        {
            var p = pairs[i];

            int defenderId;
            if (p.A == ballCarrierId)
                defenderId = p.B;
            else if (p.B == ballCarrierId)
                defenderId = p.A;
            else
                continue;

            if (p.DistSq > radiusSq)
                continue;

            // Must be opposing teams.
            if (!TryGetTeam(world, ballCarrierId, out var bcTeam) || !TryGetTeam(world, defenderId, out var defTeam))
                continue;
            if (bcTeam.TeamIndex == defTeam.TeamIndex)
                continue;
            if (defTeam.IsOffense)
                continue;

            var ev = new TackleContactEvent(defenderId, ballCarrierId, p.ContactPosition);
            SimEventBus.Send(ref ev);
        }
    }

    private void EmitBlockContacts(World world, List<Pair> pairs, int ballCarrierId)
    {
        var radiusSq = BlockContactRadius * BlockContactRadius;

        for (var i = 0; i < pairs.Count; i++)
        {
            var p = pairs[i];
            if (p.DistSq > radiusSq)
                continue;

            // Avoid block contacts involving the ball carrier.
            if (p.A == ballCarrierId || p.B == ballCarrierId)
                continue;

            if (!TryGetTeam(world, p.A, out var teamA) || !TryGetTeam(world, p.B, out var teamB))
                continue;
            if (teamA.TeamIndex == teamB.TeamIndex)
                continue;

            // offense vs defense
            if (teamA.IsOffense && !teamB.IsOffense)
            {
                var ev = new BlockContactEvent(BlockerId: p.A, DefenderId: p.B, Position: p.ContactPosition);
                SimEventBus.Send(ref ev);
            }
            else if (!teamA.IsOffense && teamB.IsOffense)
            {
                var ev = new BlockContactEvent(BlockerId: p.B, DefenderId: p.A, Position: p.ContactPosition);
                SimEventBus.Send(ref ev);
            }
        }
    }

    private static bool TryGetTeam(World world, int entityId, out Team team)
    {
        team = default;
        if (entityId < 0)
            return false;

        var found = false;
        var local = default(Team);

        var q = new QueryDescription().WithAll<Team>();
        world.Query(in q, (Entity e, ref Team t) =>
        {
            if (found)
                return;
            if (e.Id != entityId)
                return;

            local = t;
            found = true;
        });

        if (!found)
            return false;

        team = local;
        return true;
    }
}
