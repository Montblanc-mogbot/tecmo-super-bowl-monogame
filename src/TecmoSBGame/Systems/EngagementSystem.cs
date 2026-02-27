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
            if (!_behavior.Has(a) || !_behavior.Has(b) || !_stack.Has(a) || !_stack.Has(b) || !_engagement.Has(a) || !_engagement.Has(b))
                return;

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
}
