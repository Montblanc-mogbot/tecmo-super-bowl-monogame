using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// TEMPORARY fumble trigger:
/// - Watches for <see cref="WhistleEvent"/> with reason "tackle" (published by <see cref="WhistleOnTackleSystem"/>).
/// - If the ball is currently held, deterministically decides whether the carrier fumbles.
///
/// TODO: Rewire to real tackle resolution once tackle rules/animations are authoritative.
/// </summary>
public sealed class FumbleOnTackleWhistleSystem : EntityUpdateSystem
{
    private readonly GameEvents _events;
    private readonly PlayState _play;

    private ComponentMapper<BallComponent> _ballTag = null!;
    private ComponentMapper<BallStateComponent> _ballState = null!;
    private ComponentMapper<BallOwnerComponent> _ballOwner = null!;
    private ComponentMapper<PlayerAttributesComponent> _attrs = null!;

    // Tuning constants (small + deterministic).
    // Higher BASE => more fumbles.
    public const float BASE_FUMBLE_CHANCE = 0.06f;
    // Ball control reduces chance; 0..100 assumed.
    public const float BC_REDUCTION_AT_100 = 0.045f;

    public FumbleOnTackleWhistleSystem(GameEvents events, PlayState playState) : base(Aspect.All(typeof(PositionComponent)))
    {
        _events = events;
        _play = playState;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _ballTag = mapperService.GetMapper<BallComponent>();
        _ballState = mapperService.GetMapper<BallStateComponent>();
        _ballOwner = mapperService.GetMapper<BallOwnerComponent>();
        _attrs = mapperService.GetMapper<PlayerAttributesComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        // Read (do not drain) so we don't interfere with other consumers.
        var whistles = _events.Read<WhistleEvent>();
        var sawTackle = false;
        for (var i = 0; i < whistles.Count; i++)
        {
            if (string.Equals(whistles[i].Reason, "tackle", StringComparison.OrdinalIgnoreCase))
            {
                sawTackle = true;
                break;
            }
        }

        if (!sawTackle)
            return;

        var ballId = FindBallEntityId();
        if (ballId is null)
            return;

        var bid = ballId.Value;
        if (_ballState.Get(bid).State != BallState.Held)
            return;

        var carrierId = _ballOwner.Get(bid).OwnerEntityId;
        if (carrierId is null)
            return;

        var bc = _attrs.Has(carrierId.Value) ? _attrs.Get(carrierId.Value).Bc : 50;
        var bc01 = MathHelper.Clamp(bc / 100f, 0f, 1f);

        var chance = BASE_FUMBLE_CHANCE - (BC_REDUCTION_AT_100 * bc01);
        chance = MathHelper.Clamp(chance, 0.005f, 0.25f);

        var u = DeterministicFloat01((uint)_play.PlayId, (uint)carrierId.Value, 0xF00B1Eu);
        if (u < chance)
            _events.Publish(new FumbleEvent(carrierId.Value, "tackle"));
    }

    private int? FindBallEntityId()
    {
        foreach (var id in ActiveEntities)
        {
            if (_ballTag.Has(id))
                return id;
        }

        return null;
    }

    private static float DeterministicFloat01(uint playId, uint a, uint salt)
    {
        // Tiny hash/xorshift -> [0,1). Deterministic across platforms.
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
