using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Applies the state transition for a fumble:
/// - clears ownership
/// - sets ball state to Loose
/// - applies a small deterministic scatter velocity
/// - updates <see cref="PlayState"/>
/// </summary>
public sealed class FumbleResolutionSystem : EntityUpdateSystem
{
    private readonly GameEvents _events;
    private readonly PlayState _play;

    private ComponentMapper<BallComponent> _ball = null!;
    private ComponentMapper<VelocityComponent> _vel = null!;
    private ComponentMapper<BallCarrierComponent> _carrier = null!;

    // Scatter tuning.
    public const float SCATTER_SPEED = 45f;

    public FumbleResolutionSystem(GameEvents events, PlayState playState) : base(Aspect.All(typeof(PositionComponent)))
    {
        _events = events;
        _play = playState;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _ball = mapperService.GetMapper<BallComponent>();
        _vel = mapperService.GetMapper<VelocityComponent>();
        _carrier = mapperService.GetMapper<BallCarrierComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        var ballId = FindBallEntityId();
        if (ballId is null)
        {
            // Still drain so headless logs don't pile up.
            _events.Drain<FumbleEvent>(_ => { });
            return;
        }

        var bid = ballId.Value;

        _events.Drain<FumbleEvent>(e =>
        {
            var b = _ball.Get(bid);

            // Idempotence: only apply if the ball is currently held by this carrier.
            if (b.State != BallState.Held)
                return;

            var currentOwner = b.OwnerEntityId;
            if (currentOwner is null || currentOwner.Value != e.CarrierId)
                return;

            if (_carrier.Has(e.CarrierId))
                _carrier.Get(e.CarrierId).HasBall = false;

            b.OwnerEntityId = null;
            b.State = BallState.Loose;
            b.FlightKind = BallFlightKind.None;
            b.DurationSeconds = 0f;
            b.ElapsedSeconds = 0f;
            b.Height = 0f;
            b.IsComplete = false;

            // Deterministic scatter direction based on play + carrier.
            var v = GetScatterVelocity(_play.PlayId, e.CarrierId);
            if (_vel.Has(bid))
                _vel.Get(bid).Velocity = v;

            _play.BallState = BallState.Loose;
            _play.BallOwnerEntityId = null;

            // Intentionally no whistle here; loose ball play continues (for now).
            // TODO: add out-of-bounds/dead-ball rules for loose balls.
        });
    }

    private int? FindBallEntityId()
    {
        foreach (var id in ActiveEntities)
        {
            if (_ball.Has(id))
                return id;
        }

        return null;
    }

    private static Vector2 GetScatterVelocity(int playId, int carrierId)
    {
        var u = DeterministicFloat01((uint)playId, (uint)carrierId, 0xBADC0FFEu);
        // Angle in [-pi, pi)
        var angle = (u * MathF.Tau) - MathF.PI;
        var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        if (dir.LengthSquared() < 0.0001f)
            dir = new Vector2(1f, 0f);
        return dir * SCATTER_SPEED;
    }

    private static float DeterministicFloat01(uint playId, uint a, uint salt)
    {
        uint x = 0x9E3779B9u;
        x ^= playId + 0x7F4A7C15u + (x << 6) + (x >> 2);
        x ^= a + 0x165667B1u + (x << 6) + (x >> 2);
        x ^= salt + 0xD3A2646Cu + (x << 6) + (x >> 2);

        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;

        return (x & 0x00FFFFFFu) / 16777216f;
    }
}
