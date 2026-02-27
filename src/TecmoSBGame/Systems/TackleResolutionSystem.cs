using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.Field;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

public enum TackleOutcome
{
    Downed = 0,
    BrokenTackle = 1,
    Stumble = 2,
    FallForward = 3,
}

/// <summary>
/// Clean-room tackle resolution using ratings (carrier vs tackler).
///
/// Consumes <see cref="TackleContactEvent"/> and resolves at most once per tackler/carrier pair
/// within a short cooldown window.
///
/// Deterministic: uses a tiny hash-based RNG seeded by playId + entity ids.
///
/// Notes:
/// - This is not meant to be perfect Tecmo parity yet; it's a deterministic scaffold.
/// - We keep tuning constants centralized in <see cref="TackleTuning"/>.
/// </summary>
public sealed class TackleResolutionSystem : EntityUpdateSystem
{
    private readonly GameEvents _events;
    private readonly MatchState _match;
    private readonly PlayState _play;

    private ComponentMapper<PlayerAttributesComponent> _attrs = null!;
    private ComponentMapper<BehaviorComponent> _behavior = null!;
    private ComponentMapper<BehaviorStackComponent> _stack = null!;
    private ComponentMapper<SpeedModifierComponent> _speedMod = null!;

    // (tackler, carrier) -> remaining cooldown seconds
    private readonly Dictionary<ulong, float> _cooldowns = new(capacity: 64);

    public TackleResolutionSystem(GameEvents events, MatchState match, PlayState play)
        : base(Aspect.All(typeof(PlayerAttributesComponent)))
    {
        _events = events;
        _match = match;
        _play = play;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _attrs = mapperService.GetMapper<PlayerAttributesComponent>();
        _behavior = mapperService.GetMapper<BehaviorComponent>();
        _stack = mapperService.GetMapper<BehaviorStackComponent>();
        _speedMod = mapperService.GetMapper<SpeedModifierComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (dt > 0f)
            TickCooldowns(dt);

        // If the play is already over, ignore further contacts.
        if (_play.WhistleReason != WhistleReason.None)
            return;

        _events.Drain<TackleContactEvent>(evt =>
        {
            if (_play.WhistleReason != WhistleReason.None)
                return;

            var tacklerId = evt.DefenderId;
            var carrierId = evt.BallCarrierId;
            if (tacklerId == carrierId)
                return;

            var key = PairKey(tacklerId, carrierId);
            if (_cooldowns.TryGetValue(key, out var cd) && cd > 0f)
                return;
            _cooldowns[key] = TackleTuning.ContactCooldownSeconds;

            // Ratings default to 50 if missing.
            var tackler = _attrs.Has(tacklerId) ? _attrs.Get(tacklerId) : null;
            var carrier = _attrs.Has(carrierId) ? _attrs.Get(carrierId) : null;

            var tacklerHp = tackler?.Hp ?? 50;
            var tacklerRs = tackler?.Rs ?? 50;
            var tacklerMs = tackler?.Ms ?? 50;

            var carrierHp = carrier?.Hp ?? 50;
            var carrierRs = carrier?.Rs ?? 50;
            var carrierMs = carrier?.Ms ?? 50;

            var (outcome, pDown, pStumble, u) = ResolveOutcome(
                playId: _play.PlayId,
                tacklerId: tacklerId,
                carrierId: carrierId,
                tacklerHp: tacklerHp,
                tacklerRs: tacklerRs,
                tacklerMs: tacklerMs,
                carrierHp: carrierHp,
                carrierRs: carrierRs,
                carrierMs: carrierMs);

            Console.WriteLine($"[tackle] resolve tackler={tacklerId} carrier={carrierId} u={u:0.000} pDown={pDown:0.000} pStumble={pStumble:0.000} outcome={outcome}");

            switch (outcome)
            {
                case TackleOutcome.Downed:
                case TackleOutcome.FallForward:
                    EndPlayOnTackle(tacklerId, carrierId, evt.Position, outcome);
                    break;

                case TackleOutcome.Stumble:
                    ApplyStumble(carrierId);
                    // Let the existing tackle interrupt run its course, but shorten it a bit so motion resumes.
                    ShortenTackleInterruptIfPresent(tacklerId);
                    ShortenTackleInterruptIfPresent(carrierId);
                    break;

                case TackleOutcome.BrokenTackle:
                    // Break tackle: immediately clear the tackle interrupt if present.
                    ClearTackleInterruptIfPresent(tacklerId);
                    ClearTackleInterruptIfPresent(carrierId);
                    break;
            }
        });
    }

    private static (TackleOutcome Outcome, float pDown, float pStumble, float u) ResolveOutcome(
        int playId,
        int tacklerId,
        int carrierId,
        int tacklerHp,
        int tacklerRs,
        int tacklerMs,
        int carrierHp,
        int carrierRs,
        int carrierMs)
    {
        // Weighted scores.
        // Tackler: HP is primary; speed helps close/finish.
        var tacklerScore = (tacklerHp * TackleTuning.TacklerHpWeight)
                           + (tacklerRs * TackleTuning.TacklerRsWeight)
                           + (tacklerMs * TackleTuning.TacklerMsWeight);

        // Carrier: HP resists; speed makes it harder to wrap.
        var carrierResist = (carrierHp * TackleTuning.CarrierHpWeight)
                            + (carrierRs * TackleTuning.CarrierRsWeight)
                            + (carrierMs * TackleTuning.CarrierMsWeight);

        tacklerScore = MathF.Max(1f, tacklerScore);
        carrierResist = MathF.Max(1f, carrierResist);

        var pDown = tacklerScore / (tacklerScore + carrierResist);
        pDown = MathHelper.Clamp(pDown + TackleTuning.DownBaseBias, 0.02f, 0.98f);

        // If the tackle isn't a full down, we sometimes get a stumble.
        // Bias stumble upward when the tackler is close to winning.
        var closeness = MathF.Abs(pDown - 0.5f) * 2f; // 0..1
        var pStumble = TackleTuning.StumbleBase + (TackleTuning.StumbleClosenessBonus * (1f - closeness));
        pStumble = MathHelper.Clamp(pStumble, 0f, 0.40f);

        var u = DeterministicFloat01((uint)playId, (uint)tacklerId, (uint)carrierId, 0xC01AC7u);

        if (u < pDown)
        {
            // Within downed, allow a fall-forward sub-outcome.
            var u2 = DeterministicFloat01((uint)playId, (uint)carrierId, (uint)tacklerId, 0xF411F0D0u);
            if (u2 < TackleTuning.FallForwardChance)
                return (TackleOutcome.FallForward, pDown, pStumble, u);
            return (TackleOutcome.Downed, pDown, pStumble, u);
        }

        if (u < pDown + pStumble)
            return (TackleOutcome.Stumble, pDown, pStumble, u);

        return (TackleOutcome.BrokenTackle, pDown, pStumble, u);
    }

    private void EndPlayOnTackle(int tacklerId, int carrierId, Vector2 contactPos, TackleOutcome outcome)
    {
        // Compute end spot.
        var endAbs = FieldBounds.XToAbsoluteYard(contactPos.X);

        if (outcome == TackleOutcome.FallForward)
        {
            var extra = ComputeFallForwardYards(tacklerId, carrierId);
            var dir = _match.OffenseDirection;
            endAbs = dir == OffenseDirection.LeftToRight
                ? endAbs + extra
                : endAbs - extra;
        }

        endAbs = Math.Clamp(endAbs, 0, 100);

        _play.EndAbsoluteYard = endAbs;

        var startDist = PlayState.DistFromOwnGoal(_play.StartAbsoluteYard, _match.OffenseDirection);
        var endDist = PlayState.DistFromOwnGoal(endAbs, _match.OffenseDirection);
        _play.Result = _play.Result with { YardsGained = endDist - startDist };

        _play.WhistleReason = WhistleReason.Tackle;
        _play.Phase = PlayPhase.PostPlay;
        _play.BallState = BallState.Dead;
        _play.BallOwnerEntityId = carrierId;

        // Publish events for other systems (fumbles/etc). Whistle is the authoritative play-end signal.
        _events.Publish(new TackleEvent(tacklerId, carrierId, contactPos));
        _events.Publish(new WhistleEvent("tackle"));

        // Placeholder turnover hook (future: fumble/strip on hit strength).
        // if (ShouldForceFumble(...)) _events.Publish(new FumbleEvent(carrierId, "tackle"));

        Console.WriteLine($"[tackle] whistle carrier={carrierId} tackler={tacklerId} outcome={outcome} endAbs={endAbs}");
    }

    private int ComputeFallForwardYards(int tacklerId, int carrierId)
    {
        // Small deterministic bump: 0-2 yards.
        var u = DeterministicFloat01((uint)_play.PlayId, (uint)carrierId, (uint)tacklerId, 0xF411B4C0u);
        if (u < 0.20f)
            return 2;
        if (u < 0.70f)
            return 1;
        return 0;
    }

    private void ApplyStumble(int carrierId)
    {
        if (!_speedMod.Has(carrierId))
            return;

        var m = _speedMod.Get(carrierId);
        m.MaxSpeedMultiplier = TackleTuning.StumbleSpeedMultiplier;
        m.TimerSeconds = MathF.Max(m.TimerSeconds, TackleTuning.StumbleDurationSeconds);
    }

    private void ClearTackleInterruptIfPresent(int entityId)
    {
        if (!_behavior.Has(entityId) || !_stack.Has(entityId))
            return;

        var stack = _stack.Get(entityId);
        if (!stack.HasActive(BehaviorInterruptKind.Tackle))
            return;

        if (!stack.TryPop(out var popped))
            return;

        var b = _behavior.Get(entityId);
        BehaviorInterrupt.Restore(b, popped.Saved);

        Console.WriteLine($"[interrupt] break kind=Tackle entity={entityId}");
    }

    private void ShortenTackleInterruptIfPresent(int entityId)
    {
        if (!_stack.Has(entityId))
            return;

        var stack = _stack.Get(entityId);
        if (!stack.TryPeek(out var top))
            return;

        if (top.Kind != BehaviorInterruptKind.Tackle)
            return;

        var remaining = MathF.Min(top.RemainingSeconds, TackleTuning.StumbleInterruptMaxRemainingSeconds);
        stack.Stack[^1] = top with { RemainingSeconds = remaining };
    }

    private void TickCooldowns(float dt)
    {
        if (_cooldowns.Count == 0)
            return;

        // Deterministic iteration: copy keys into temp list, sort.
        // (Dictionary iteration order is not deterministic across runtimes.)
        var keys = new List<ulong>(_cooldowns.Keys);
        keys.Sort();

        for (var i = 0; i < keys.Count; i++)
        {
            var k = keys[i];
            var t = _cooldowns[k] - dt;
            if (t <= 0f)
                _cooldowns.Remove(k);
            else
                _cooldowns[k] = t;
        }
    }

    private static ulong PairKey(int a, int b) => ((ulong)(uint)a << 32) | (uint)b;

    private static float DeterministicFloat01(uint playId, uint a, uint b, uint salt)
    {
        uint x = 0x9E3779B9u;
        x ^= playId + 0x7F4A7C15u + (x << 6) + (x >> 2);
        x ^= a + 0x165667B1u + (x << 6) + (x >> 2);
        x ^= b + 0xD3A2646Cu + (x << 6) + (x >> 2);
        x ^= salt + 0xFD7046C5u + (x << 6) + (x >> 2);

        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;

        return (x & 0x00FFFFFFu) / 16777216f;
    }
}

public static class TackleTuning
{
    // Cooldown so we resolve at most once per tackler/carrier during a contact window.
    public const float ContactCooldownSeconds = 0.20f;

    // Rating weights.
    public const float TacklerHpWeight = 1.25f;
    public const float TacklerRsWeight = 0.35f;
    public const float TacklerMsWeight = 0.25f;

    public const float CarrierHpWeight = 1.15f;
    public const float CarrierRsWeight = 0.25f;
    public const float CarrierMsWeight = 0.55f;

    // Small global nudge so average tackles complete a bit more often.
    public const float DownBaseBias = 0.02f;

    // Non-downed outcomes.
    public const float StumbleBase = 0.08f;
    public const float StumbleClosenessBonus = 0.12f;

    public const float StumbleSpeedMultiplier = 0.65f;
    public const float StumbleDurationSeconds = 0.55f;

    // If we stumble/bounce, don't stay frozen in tackle interrupt for the full duration.
    public const float StumbleInterruptMaxRemainingSeconds = 0.12f;

    // Downed sub-outcome.
    public const float FallForwardChance = 0.18f;
}
