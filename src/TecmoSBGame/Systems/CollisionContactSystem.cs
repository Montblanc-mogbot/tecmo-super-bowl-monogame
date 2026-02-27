using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Discrete, distance-based collision/contact checks.
///
/// Layered phases (still a single system for now):
/// 1) Proximity detection: gather candidate pairs within a radius.
/// 2) Tackle eligibility: defender vs ball-carrier contact candidates.
/// 3) Block engagement: offense (blocker) vs defense contact candidates.
///
/// This system emits events only (no positional resolution) to keep simulation deterministic
/// and allow downstream gameplay systems to decide consequences.
/// </summary>
public sealed class CollisionContactSystem : EntityUpdateSystem
{
    private readonly GameEvents _events;
    private readonly LoopState? _loop;

    private ComponentMapper<PositionComponent> _pos;
    private ComponentMapper<TeamComponent> _team;
    private ComponentMapper<BallCarrierComponent> _ball;

    // Radii/constants (NES pixel-ish units).
    public const float PROXIMITY_RADIUS = 12f;

    public const float TACKLE_CONTACT_RADIUS_BASE = 8f;
    public const float TACKLE_CONTACT_RADIUS_ATTEMPT_BONUS = 2f;

    public const float BLOCK_CONTACT_RADIUS = 12f;

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

    public CollisionContactSystem(GameEvents events, LoopState? loop = null)
        : base(Aspect.All(typeof(PositionComponent), typeof(TeamComponent)))
    {
        _events = events;
        _loop = loop;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _pos = mapperService.GetMapper<PositionComponent>();
        _team = mapperService.GetMapper<TeamComponent>();
        _ball = mapperService.GetMapper<BallCarrierComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        // Only evaluate contacts during live play (pre-snap should be static).
        if (_loop is not null && !_loop.IsOnField("live_play"))
            return;

        // Deterministic iteration order.
        var entities = new List<int>(ActiveEntities);
        entities.Sort();

        // Find current ball carrier (if any).
        var ballCarrierId = -1;
        for (var i = 0; i < entities.Count; i++)
        {
            var id = entities[i];
            if (_ball.Has(id) && _ball.Get(id).HasBall)
            {
                ballCarrierId = id;
                break;
            }
        }

        // Gather tackle attempts for this tick (for distance bonus).
        // Map: tacklerId -> attempted ballCarrierId.
        var attempts = new Dictionary<int, int>(capacity: 4);
        var attemptEvents = _events.Read<TackleAttemptEvent>();
        for (var i = 0; i < attemptEvents.Count; i++)
        {
            var a = attemptEvents[i];
            attempts[a.TacklerId] = a.BallCarrierId;
        }

        // Phase 1: proximity candidate pairs.
        var pairs = GatherProximityPairs(entities, PROXIMITY_RADIUS);

        // Phase 2: tackle eligibility/contact.
        if (ballCarrierId != -1)
            EmitTackleContacts(pairs, ballCarrierId, attempts);

        // Phase 3: block engagement/contact.
        EmitBlockContacts(pairs, ballCarrierId);
    }

    private List<Pair> GatherProximityPairs(List<int> entities, float radius)
    {
        var radiusSq = radius * radius;
        var pairs = new List<Pair>(capacity: Math.Max(4, entities.Count));

        for (var i = 0; i < entities.Count; i++)
        {
            var a = entities[i];
            var posA = _pos.Get(a).Position;

            for (var j = i + 1; j < entities.Count; j++)
            {
                var b = entities[j];
                var posB = _pos.Get(b).Position;

                var dx = posB.X - posA.X;
                var dy = posB.Y - posA.Y;
                var distSq = (dx * dx) + (dy * dy);

                if (distSq > radiusSq)
                    continue;

                // Contact position for downstream systems (midpoint is deterministic and stable).
                var contactPos = (posA + posB) * 0.5f;
                pairs.Add(new Pair(a, b, distSq, contactPos));
            }
        }

        return pairs;
    }

    private void EmitTackleContacts(List<Pair> pairs, int ballCarrierId, Dictionary<int, int> attempts)
    {
        for (var i = 0; i < pairs.Count; i++)
        {
            var p = pairs[i];

            // Only pairs that include the ball carrier.
            int defenderId;
            if (p.A == ballCarrierId)
                defenderId = p.B;
            else if (p.B == ballCarrierId)
                defenderId = p.A;
            else
                continue;

            // Must be opposing teams.
            var bcTeam = _team.Get(ballCarrierId);
            var defTeam = _team.Get(defenderId);
            if (bcTeam.TeamIndex == defTeam.TeamIndex)
                continue;

            // Defender must be on defense.
            if (defTeam.IsOffense)
                continue;

            var radius = TACKLE_CONTACT_RADIUS_BASE;
            if (attempts.TryGetValue(defenderId, out var attemptedCarrier) && attemptedCarrier == ballCarrierId)
                radius += TACKLE_CONTACT_RADIUS_ATTEMPT_BONUS;

            if (p.DistSq <= radius * radius)
                _events.Publish(new TackleContactEvent(defenderId, ballCarrierId, p.ContactPosition));
        }
    }

    private void EmitBlockContacts(List<Pair> pairs, int ballCarrierId)
    {
        var radiusSq = BLOCK_CONTACT_RADIUS * BLOCK_CONTACT_RADIUS;

        for (var i = 0; i < pairs.Count; i++)
        {
            var p = pairs[i];
            if (p.DistSq > radiusSq)
                continue;

            // Avoid generating block contacts involving the ball carrier (they can be tackled instead).
            if (p.A == ballCarrierId || p.B == ballCarrierId)
                continue;

            var teamA = _team.Get(p.A);
            var teamB = _team.Get(p.B);
            if (teamA.TeamIndex == teamB.TeamIndex)
                continue;

            // Block engagement is offense-vs-defense.
            if (teamA.IsOffense && !teamB.IsOffense)
                _events.Publish(new BlockContactEvent(BlockerId: p.A, DefenderId: p.B, Position: p.ContactPosition));
            else if (!teamA.IsOffense && teamB.IsOffense)
                _events.Publish(new BlockContactEvent(BlockerId: p.B, DefenderId: p.A, Position: p.ContactPosition));
        }
    }
}
