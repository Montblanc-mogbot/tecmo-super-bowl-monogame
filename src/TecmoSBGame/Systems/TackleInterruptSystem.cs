using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;

namespace TecmoSBGame.Systems;

/// <summary>
/// Consumes <see cref="TackleContactEvent"/> and temporarily interrupts the carrier + defender.
/// This is scaffolding for later tackle resolution (whistles, break tackles, etc.).
/// </summary>
public sealed class TackleInterruptSystem : EntityUpdateSystem
{
    private readonly GameEvents _events;

    private ComponentMapper<BehaviorComponent> _behavior = null!;
    private ComponentMapper<BehaviorStackComponent> _stack = null!;

    public const float TACKLE_INTERRUPT_DURATION_SECONDS = 0.50f;

    public TackleInterruptSystem(GameEvents events)
        : base(Aspect.All(typeof(BehaviorComponent), typeof(BehaviorStackComponent)))
    {
        _events = events;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _behavior = mapperService.GetMapper<BehaviorComponent>();
        _stack = mapperService.GetMapper<BehaviorStackComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        _events.Drain<TackleContactEvent>(evt =>
        {
            var tacklerId = evt.DefenderId;
            var carrierId = evt.BallCarrierId;

            if (!_behavior.Has(tacklerId) || !_behavior.Has(carrierId) || !_stack.Has(tacklerId) || !_stack.Has(carrierId))
                return;

            // Gate: don't re-interrupt if already in an active tackle interrupt.
            if (_stack.Get(tacklerId).HasActive(BehaviorInterruptKind.Tackle) || _stack.Get(carrierId).HasActive(BehaviorInterruptKind.Tackle))
                return;

            BeginTackleInterrupt(tacklerId, carrierId);
        });
    }

    private void BeginTackleInterrupt(int tacklerId, int carrierId)
    {
        InterruptInto(tacklerId, carrierId, BehaviorState.Tackling);
        InterruptInto(carrierId, tacklerId, BehaviorState.Grappling);

        Console.WriteLine($"[interrupt] begin kind=Tackle tackler={tacklerId} carrier={carrierId}");
    }

    private void InterruptInto(int entityId, int targetId, BehaviorState newState)
    {
        var behavior = _behavior.Get(entityId);
        var stack = _stack.Get(entityId);

        BehaviorInterrupt.Push(
            behavior,
            stack,
            BehaviorInterruptKind.Tackle,
            durationSeconds: TACKLE_INTERRUPT_DURATION_SECONDS);

        behavior.State = newState;
        behavior.StateTimer = TACKLE_INTERRUPT_DURATION_SECONDS;
        behavior.TargetEntityId = targetId;
    }
}
