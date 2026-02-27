using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Fixed-tick driver for the YAML-defined loop machines.
///
/// This system is meant to run once per simulation tick (60Hz) and update
/// <see cref="LoopState"/>, which other systems can consult for gating.
///
/// We keep it as an ECS system so it participates in deterministic ordering.
/// Place it late in the update order so it can react to events published by
/// earlier systems in the same tick.
/// </summary>
public sealed class LoopMachineSystem : EntityUpdateSystem
{
    private readonly GameEvents? _events;
    private readonly LoopState _loop;

    public LoopMachineSystem(LoopState loop, GameEvents? events = null)
        : base(Aspect.All(typeof(PositionComponent)))
    {
        _loop = loop ?? throw new ArgumentNullException(nameof(loop));
        _events = events;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        // No component mappers needed.
    }

    public override void Update(GameTime gameTime)
    {
        // Bridge deterministic gameplay events into the YAML on-field loop.
        // Use Read() (not Drain) to avoid interfering with other consumers.
        if (_events is not null)
        {
            var snaps = _events.Read<SnapEvent>();
            if (snaps.Count > 0)
            {
                // Any snap transitions us into live play via the YAML snap->live_play next.
                _loop.OnFieldLoop.RaiseEvent("snap");
            }

            var whistles = _events.Read<WhistleEvent>();
            for (var i = 0; i < whistles.Count; i++)
            {
                var reason = whistles[i].Reason;
                if (!string.IsNullOrWhiteSpace(reason))
                    _loop.OnFieldLoop.RaiseEvent(reason);
            }
        }

        // Tick machines (one tick == one fixed simulation tick).
        _loop.GameLoop.Tick();
        _loop.OnFieldLoop.Tick();

        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _loop.Advance(dt);
    }
}
