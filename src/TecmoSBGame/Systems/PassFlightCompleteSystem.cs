using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// After <see cref="BallPhysicsSystem"/> updates the parametric flight, this system resolves
/// a completed pass flight into deterministic catch / interception / incompletion.
/// </summary>
public sealed class PassFlightCompleteSystem : EntityUpdateSystem
{
    private readonly GameEvents? _events;
    private readonly PlayState? _play;
    private readonly PassResolutionRuleset _ruleset;

    private ComponentMapper<BallComponent> _ballTag = null!;
    private ComponentMapper<BallStateComponent> _ballState = null!;
    private ComponentMapper<BallOwnerComponent> _ballOwner = null!;
    private ComponentMapper<BallFlightComponent> _flight = null!;
    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<BallCarrierComponent> _carrier = null!;
    private ComponentMapper<TeamComponent> _team = null!;
    private ComponentMapper<PlayerAttributesComponent> _attrs = null!;

    // Placeholder tuning constants (keep small + deterministic).
    // NOTE: In CleanRoomApprox mode these are tuned by feel. In AssemblyParity mode these
    // should be replaced by the original game's tables/thresholds.
    private const float ELIGIBLE_RADIUS = 14f; // in field units
    private const int INCOMPLETE_BASE = 120;   // higher => more incompletions

    public PassFlightCompleteSystem(
        GameEvents? events = null,
        PlayState? playState = null,
        PassResolutionRuleset ruleset = PassResolutionRuleset.CleanRoomApprox)
        : base(Aspect.All(typeof(PositionComponent)))
    {
        _events = events;
        _play = playState;
        _ruleset = ruleset;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _ballTag = mapperService.GetMapper<BallComponent>();
        _ballState = mapperService.GetMapper<BallStateComponent>();
        _ballOwner = mapperService.GetMapper<BallOwnerComponent>();
        _flight = mapperService.GetMapper<BallFlightComponent>();
        _pos = mapperService.GetMapper<PositionComponent>();
        _carrier = mapperService.GetMapper<BallCarrierComponent>();
        _team = mapperService.GetMapper<TeamComponent>();
        _attrs = mapperService.GetMapper<PlayerAttributesComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        // Find the authoritative ball entity.
        int? ballEntityId = null;
        foreach (var id in ActiveEntities)
        {
            if (_ballTag.Has(id))
            {
                ballEntityId = id;
                break;
            }
        }

        if (ballEntityId is null)
            return;

        var ballId = ballEntityId.Value;

        if (!_flight.Has(ballId))
            return;

        var f = _flight.Get(ballId);
        if (f.Kind != BallFlightKind.Pass || !f.IsComplete)
            return;

        // Safety: if no passer, treat as incomplete.
        if (f.PasserId is not int passerId || !_team.Has(passerId))
        {
            ResolveIncomplete(ballId, passerId: f.PasserId ?? -1, f.TargetId, _pos.Get(ballId).Position);
            ClearPassFlight(f);
            return;
        }

        var ballPos = _pos.Get(ballId).Position;
        var passerTeam = _team.Get(passerId);

        // Eligible players are those within a small radius of the ball at completion.
        var radiusSq = ELIGIBLE_RADIUS * ELIGIBLE_RADIUS;

        int? intendedReceiverId = f.TargetId;

        int? chosenReceiverId = null;
        float chosenReceiverDistSq = float.PositiveInfinity;

        int? bestDefenderId = null;
        float bestDefenderScore = float.NegativeInfinity;

        foreach (var id in ActiveEntities)
        {
            if (id == ballId)
                continue;

            if (!_pos.Has(id) || !_team.Has(id))
                continue;

            var d = _pos.Get(id).Position - ballPos;
            var distSq = d.LengthSquared();
            if (distSq > radiusSq)
                continue;

            var t = _team.Get(id);

            // Receiver candidates: same team, offense.
            if (t.TeamIndex == passerTeam.TeamIndex && t.IsOffense)
            {
                // Prefer the intended target if eligible; otherwise choose nearest eligible offense.
                if (intendedReceiverId is int targetId && id == targetId)
                {
                    chosenReceiverId = id;
                    chosenReceiverDistSq = distSq;
                }
                else if (chosenReceiverId is null)
                {
                    // First eligible fallback receiver.
                    chosenReceiverId = id;
                    chosenReceiverDistSq = distSq;
                }
                else
                {
                    // If we already picked a fallback (and still haven't seen the intended target), prefer nearer.
                    if (distSq < chosenReceiverDistSq - 0.0001f || (MathF.Abs(distSq - chosenReceiverDistSq) <= 0.0001f && id < chosenReceiverId.Value))
                    {
                        chosenReceiverId = id;
                        chosenReceiverDistSq = distSq;
                    }
                }

                continue;
            }

            // Defender candidates: opposite side (not offense).
            if (!t.IsOffense)
            {
                var score = GetAdjustedCatchScore(id, distSq);
                if (score > bestDefenderScore + 0.0001f || (MathF.Abs(score - bestDefenderScore) <= 0.0001f && (bestDefenderId is null || id < bestDefenderId.Value)))
                {
                    bestDefenderScore = score;
                    bestDefenderId = id;
                }
            }
        }

        // If the intended target is not eligible but a different receiver was, we still resolve against that receiver.
        if (chosenReceiverId is null)
        {
            ResolveIncomplete(ballId, passerId, f.TargetId, ballPos);
            ClearPassFlight(f);
            return;
        }

        var receiverId = chosenReceiverId.Value;
        var receiverDistSq = chosenReceiverDistSq;

        var receiverScore = GetAdjustedCatchScore(receiverId, receiverDistSq);
        var defenderScore = bestDefenderId is null ? 0f : MathF.Max(0f, bestDefenderScore);

        // Pass type tuning: lobs are a bit "floatier" => slightly more defender weight.
        if (f.PassType == PassType.Lob)
            defenderScore *= 1.15f;

        var incBase = INCOMPLETE_BASE;
        if (f.PassType == PassType.Lob)
            incBase = (int)(incBase * 0.90f);

        var total = receiverScore + defenderScore + incBase;
        if (total <= 0.0001f)
        {
            ResolveIncomplete(ballId, passerId, f.TargetId, ballPos);
            ClearPassFlight(f);
            return;
        }

        var pCatch = receiverScore / total;
        var pInt = defenderScore / total;

        var u = DeterministicFloat01((uint)(_play?.PlayId ?? 0), (uint)passerId, (uint)receiverId, (uint)(bestDefenderId ?? 0));

        if (u < pCatch)
        {
            ResolveCatch(ballId, passerId, f.TargetId, receiverId, ballPos);
        }
        else if (u < pCatch + pInt && bestDefenderId is int defenderId)
        {
            ResolveInterception(ballId, passerId, f.TargetId, defenderId, ballPos);
        }
        else
        {
            ResolveIncomplete(ballId, passerId, f.TargetId, ballPos);
        }

        ClearPassFlight(f);
    }

    private float GetAdjustedCatchScore(int entityId, float distSq)
    {
        // Ratings are expected to be small-ish ints; keep formula simple and deterministic.
        // Base "hands" score: Receiving weighted + Ball Control.
        var rec = _attrs.Has(entityId) ? _attrs.Get(entityId).Rec : 50;
        var bc = _attrs.Has(entityId) ? _attrs.Get(entityId).Bc : 50;

        var baseScore = MathF.Max(0f, (rec * 2f) + bc);

        // Proximity scaling: within ELIGIBLE_RADIUS, closer gets closer to 1.0.
        var dist = MathF.Sqrt(MathF.Max(0f, distSq));
        var prox = 1f - (dist / ELIGIBLE_RADIUS);
        prox = MathHelper.Clamp(prox, 0f, 1f);

        // Keep everyone at least half-strength so ratings still matter.
        var proxScale = 0.5f + (0.5f * prox);

        return baseScore * proxScale;
    }

    private void ResolveCatch(int ballId, int passerId, int? targetId, int receiverId, Vector2 ballPos)
    {
        if (_carrier.Has(receiverId))
            _carrier.Get(receiverId).HasBall = true;

        if (_carrier.Has(passerId))
            _carrier.Get(passerId).HasBall = false;

        _ballState.Get(ballId).State = BallState.Held;
        _ballOwner.Get(ballId).OwnerEntityId = receiverId;

        if (_play is not null)
        {
            _play.BallState = BallState.Held;
            _play.BallOwnerEntityId = receiverId;
        }

        if (_events is not null)
        {
            if (_pos.Has(receiverId))
                _events.Publish(new BallCaughtEvent(receiverId, _pos.Get(receiverId).Position));
            _events.Publish(new PassResolvedEvent(PassOutcome.Catch, passerId, targetId, receiverId, ballPos));
        }
    }

    private void ResolveInterception(int ballId, int passerId, int? targetId, int defenderId, Vector2 ballPos)
    {
        if (_carrier.Has(defenderId))
            _carrier.Get(defenderId).HasBall = true;

        if (_carrier.Has(passerId))
            _carrier.Get(passerId).HasBall = false;

        if (targetId is int tid && _carrier.Has(tid))
            _carrier.Get(tid).HasBall = false;

        _ballState.Get(ballId).State = BallState.Held;
        _ballOwner.Get(ballId).OwnerEntityId = defenderId;

        if (_play is not null)
        {
            _play.BallState = BallState.Held;
            _play.BallOwnerEntityId = defenderId;
            _play.Result = _play.Result with { Turnover = true };
            // No whistle; play continues.
        }

        if (_events is not null)
            _events.Publish(new PassResolvedEvent(PassOutcome.Interception, passerId, targetId, defenderId, ballPos));
    }

    private void ResolveIncomplete(int ballId, int passerId, int? targetId, Vector2 ballPos)
    {
        _ballState.Get(ballId).State = BallState.Dead;
        _ballOwner.Get(ballId).OwnerEntityId = null;

        if (_play is not null)
        {
            _play.BallState = BallState.Dead;
            _play.BallOwnerEntityId = null;
            _play.WhistleReason = WhistleReason.Incomplete;
        }

        if (_events is not null)
        {
            _events.Publish(new WhistleEvent("incomplete"));
            _events.Publish(new PassResolvedEvent(PassOutcome.Incomplete, passerId, targetId, null, ballPos));
        }
    }

    private static void ClearPassFlight(BallFlightComponent f)
    {
        f.Kind = BallFlightKind.None;
        f.PasserId = null;
        f.TargetId = null;
        f.IsComplete = true;
        f.Height = 0f;
    }

    private static float DeterministicFloat01(uint playId, uint a, uint b, uint c)
    {
        // Tiny hash/xorshift -> [0,1). Deterministic across platforms.
        uint x = 0x9E3779B9u;
        x ^= playId + 0x7F4A7C15u + (x << 6) + (x >> 2);
        x ^= a + 0x165667B1u + (x << 6) + (x >> 2);
        x ^= b + 0xD3A2646Cu + (x << 6) + (x >> 2);
        x ^= c + 0xFD7046C5u + (x << 6) + (x >> 2);

        // xorshift32
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;

        // 24-bit mantissa style.
        return (x & 0x00FFFFFFu) / 16777216f;
    }
}
