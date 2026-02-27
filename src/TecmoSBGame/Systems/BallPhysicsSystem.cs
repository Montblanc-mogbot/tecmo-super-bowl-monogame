using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Ball-only deterministic physics:
/// - Held: glue to owner.
/// - InFlight: lerp XY from start->end + compute height parabola.
/// - Loose/InAir (no flight component): integrate constant velocity (per-60Hz tick units).
///
/// Note: Game rules (who catches the ball) live in GameStateSystem; this system only updates motion.
/// </summary>
public sealed class BallPhysicsSystem : EntityUpdateSystem
{
    private ComponentMapper<BallComponent> _ballTag = null!;
    private ComponentMapper<BallStateComponent> _state = null!;
    private ComponentMapper<BallOwnerComponent> _owner = null!;
    private ComponentMapper<BallFlightComponent> _flight = null!;
    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<VelocityComponent> _vel = null!;

    public BallPhysicsSystem() : base(Aspect.All(typeof(BallComponent), typeof(PositionComponent), typeof(VelocityComponent), typeof(BallStateComponent), typeof(BallOwnerComponent)))
    {
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _ballTag = mapperService.GetMapper<BallComponent>();
        _state = mapperService.GetMapper<BallStateComponent>();
        _owner = mapperService.GetMapper<BallOwnerComponent>();
        _flight = mapperService.GetMapper<BallFlightComponent>();
        _pos = mapperService.GetMapper<PositionComponent>();
        _vel = mapperService.GetMapper<VelocityComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (dt <= 0f)
            return;

        var tickScale = dt * 60f;

        foreach (var ballId in ActiveEntities)
        {
            if (!_ballTag.Has(ballId))
                continue;

            var state = _state.Get(ballId);
            var owner = _owner.Get(ballId);
            var pos = _pos.Get(ballId);
            var vel = _vel.Get(ballId);

            // Held: glue to the owner.
            if (state.State == BallState.Held && owner.OwnerEntityId is int ownerId && _pos.Has(ownerId))
            {
                pos.Position = _pos.Get(ownerId).Position;
                vel.Velocity = Vector2.Zero;
                if (_flight.Has(ballId))
                {
                    var f = _flight.Get(ballId);
                    f.Height = 0f;
                    f.IsComplete = true;
                }
                continue;
            }

            // In flight: override XY by parametric model.
            if (_flight.Has(ballId))
            {
                var f = _flight.Get(ballId);
                if (f.Kind == BallFlightKind.None)
                    goto IntegrateLoose;

                f.ElapsedSeconds = MathF.Min(f.DurationSeconds, f.ElapsedSeconds + dt);

                var s = f.DurationSeconds <= 0.0001f ? 1f : MathHelper.Clamp(f.ElapsedSeconds / f.DurationSeconds, 0f, 1f);
                pos.Position = Vector2.Lerp(f.StartPos, f.EndPos, s);

                // Visual-only height parabola.
                f.Height = 4f * f.ApexHeight * s * (1f - s);

                f.IsComplete = s >= 1f;

                // While in flight we do not use the velocity integrator.
                vel.Velocity = Vector2.Zero;
                continue;
            }

IntegrateLoose:
            // Loose or in-air without a flight component: constant velocity integration.
            // Velocity is in "units per 60Hz tick".
            if (state.State is BallState.InAir or BallState.Loose)
            {
                pos.Position += vel.Velocity * tickScale;
            }
        }
    }
}
