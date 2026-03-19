using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;

namespace TecmoSBGame.Systems;

/// <summary>
/// Example decoupled consumer/producer: turns a tackle into a whistle.
///
/// Demonstrates reacting to events without hard-wiring dependencies between gameplay systems.
/// </summary>
public sealed class WhistleOnTackleSystem : EntityUpdateSystem
{
    private readonly GameEvents _events;

    public WhistleOnTackleSystem(GameEvents events) : base(Aspect.All(typeof(PositionComponent)))
    {
        _events = events;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        // No component mappers needed.
    }

    public override void Update(GameTime gameTime)
    {
        _events.Drain<TackleEvent>(_ =>
        {
            _events.Publish(new WhistleEvent("tackle"));
        });
    }
}