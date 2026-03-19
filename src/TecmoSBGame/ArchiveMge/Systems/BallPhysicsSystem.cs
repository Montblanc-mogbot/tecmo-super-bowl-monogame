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
    private ComponentMapper<BallComponent> _ball = null!;
    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<VelocityComponent> _vel = null!;

    public BallPhysicsSystem() : base(Aspect.All(typeof(BallComponent), typeof(PositionComponent), typeof(VelocityComponent)))
    {
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _ball = mapperService.GetMapper<BallComponent>();
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
            var b = _ball.Get(ballId);
            var pos = _pos.Get(ballId);
            var vel = _vel.Get(ballId);

            // Held: glue to the owner.
            if (b.State == BallState.Held && b.OwnerEntityId is int ownerId && _pos.Has(ownerId))
            {
                pos.Position = _pos.Get(ownerId).Position;
                vel.Velocity = Vector2.Zero;

                b.Height = 0f;
                b.IsComplete = true;
                b.FlightKind = BallFlightKind.None;
                b.DurationSeconds = 0f;
                b.ElapsedSeconds = 0f;

                continue;
            }

            // In flight: override XY by parametric model.
            if (b.FlightKind != BallFlightKind.None)
            {
                b.ElapsedSeconds = MathF.Min(b.DurationSeconds, b.ElapsedSeconds + dt);

                var s = b.DurationSeconds <= 0.0001f ? 1f : MathHelper.Clamp(b.ElapsedSeconds / b.DurationSeconds, 0f, 1f);
                pos.Position = Vector2.Lerp(b.StartPos, b.EndPos, s);

                // Visual-only height parabola.
                b.Height = 4f * b.ApexHeight * s * (1f - s);

                b.IsComplete = s >= 1f;

                // While in flight we do not use the velocity integrator.
                vel.Velocity = Vector2.Zero;
                continue;
            }

IntegrateLoose:
            // Loose or in-air without a flight component: constant velocity integration.
            // Velocity is in "units per 60Hz tick".
            if (b.State is BallState.InAir or BallState.Loose)
            {
                pos.Position += vel.Velocity * tickScale;
            }
        }
    }
}
