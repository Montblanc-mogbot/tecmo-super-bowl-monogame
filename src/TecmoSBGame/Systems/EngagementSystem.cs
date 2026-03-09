using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;

namespace TecmoSBGame.Systems;

/// <summary>
/// Consumes <see cref="BlockContactEvent"/> and temporarily interrupts both entities into an Engaged state.
/// This is scaffolding for later block resolution/animations.
/// </summary>
public sealed class EngagementSystem : EntityUpdateSystem
{
    private readonly GameEvents _events;

    private ComponentMapper<BehaviorComponent> _behavior = null!;
    private ComponentMapper<BehaviorStackComponent> _stack = null!;
    private ComponentMapper<EngagementComponent> _engagement = null!;
    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<BlockTargetComponent> _blockTarget = null!;
    private ComponentMapper<VelocityComponent> _vel = null!;

    // Short, deterministic "hold" duration.
    public const float ENGAGEMENT_DURATION_SECONDS = 0.35f;

    // Cooldown prevents re-engaging every tick while still colliding.
    public const float ENGAGEMENT_COOLDOWN_SECONDS = 0.60f;

    public EngagementSystem(GameEvents events)
        : base(Aspect.All(typeof(BehaviorComponent), typeof(BehaviorStackComponent)))
    {
        _events = events;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _behavior = mapperService.GetMapper<BehaviorComponent>();
        _stack = mapperService.GetMapper<BehaviorStackComponent>();
        _engagement = mapperService.GetMapper<EngagementComponent>();
        _pos = mapperService.GetMapper<PositionComponent>();
        _blockTarget = mapperService.GetMapper<BlockTargetComponent>();
        _vel = mapperService.GetMapper<VelocityComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Tick cooldown timers.
        if (dt > 0f)
        {
            foreach (var id in ActiveEntities)
            {
                if (!_engagement.Has(id))
                    continue;

                var e = _engagement.Get(id);
                if (e.CooldownSeconds > 0f)
                {
                    e.CooldownSeconds = Math.Max(0f, e.CooldownSeconds - dt);
                    if (e.CooldownSeconds <= 0f)
                        e.PartnerEntityId = -1;
                }
            }
        }

        // Resolve contact -> engagement.
        _events.Drain<BlockContactEvent>(evt =>
        {
            var a = evt.BlockerId;
            var b = evt.DefenderId;

            // Entities must have the needed components.
            if (!_behavior.Has(a) || !_behavior.Has(b) || !_stack.Has(a) || !_stack.Has(b) || !_engagement.Has(a) || !_engagement.Has(b) || !_pos.Has(a) || !_pos.Has(b))
                return;

            // Assignment gate (Tecmo-style): only engage if the blocker has selected this defender.
            // Note: we allow defensive entities to engage without a BlockTargetComponent.
            if (_blockTarget.Has(a))
            {
                var bt = _blockTarget.Get(a);
                if (bt.TargetEntityId != b)
                    return;
            }

            // Contact distance gate (~4-6 px in disassembly notes). CollisionContactSystem uses a larger
            // proximity radius, so we re-check exact distance here.
            var posA = _pos.Get(a).Position;
            var posB = _pos.Get(b).Position;
            var distSq = Vector2.DistanceSquared(posA, posB);
            if (distSq > BlockerAISystem.CONTACT_DISTANCE_PIXELS * BlockerAISystem.CONTACT_DISTANCE_PIXELS)
                return;

            // Facing gate: a blocker can't engage a defender "behind" them.
            if (_blockTarget.Has(a))
            {
                var facing = GetFacingDir(a);
                if (facing != Vector2.Zero)
                {
                    var toDef = posB - posA;
                    if (toDef.LengthSquared() > 0.01f)
                    {
                        var dir = Vector2.Normalize(toDef);
                        if (Vector2.Dot(dir, facing) < -0.10f)
                            return;
                    }
                }
            }

            var ea = _engagement.Get(a);
            var eb = _engagement.Get(b);

            // Gate: if either is on cooldown, ignore.
            if (ea.CooldownSeconds > 0f || eb.CooldownSeconds > 0f)
                return;

            // Gate: don't stack multiple engagements.
            if (_stack.Get(a).HasActive(BehaviorInterruptKind.Engagement) || _stack.Get(b).HasActive(BehaviorInterruptKind.Engagement))
                return;

            BeginEngagement(a, b);
        });
    }

    private void BeginEngagement(int blockerId, int defenderId)
    {
        InterruptIntoEngaged(blockerId, defenderId);
        InterruptIntoEngaged(defenderId, blockerId);

        var a = _engagement.Get(blockerId);
        a.PartnerEntityId = defenderId;
        a.CooldownSeconds = ENGAGEMENT_COOLDOWN_SECONDS;

        var b = _engagement.Get(defenderId);
        b.PartnerEntityId = blockerId;
        b.CooldownSeconds = ENGAGEMENT_COOLDOWN_SECONDS;

        Console.WriteLine($"[interrupt] begin kind=Engagement blocker={blockerId} defender={defenderId}");
    }

    private void InterruptIntoEngaged(int entityId, int partnerId)
    {
        var behavior = _behavior.Get(entityId);
        var stack = _stack.Get(entityId);

        BehaviorInterrupt.Push(
            behavior,
            stack,
            BehaviorInterruptKind.Engagement,
            durationSeconds: ENGAGEMENT_DURATION_SECONDS);

        behavior.State = BehaviorState.Engaged;
        behavior.StateTimer = ENGAGEMENT_DURATION_SECONDS;
        behavior.TargetEntityId = partnerId;
    }

    private Vector2 GetFacingDir(int entityId)
    {
        if (_vel.Has(entityId))
        {
            var v = _vel.Get(entityId).Velocity;
            if (v.LengthSquared() > 0.01f)
                return Vector2.Normalize(v);
        }

        var b = _behavior.Get(entityId);
        var to = b.TargetPosition - _pos.Get(entityId).Position;
        if (to.LengthSquared() > 1f)
            return Vector2.Normalize(to);

        return Vector2.Zero;
    }
}
