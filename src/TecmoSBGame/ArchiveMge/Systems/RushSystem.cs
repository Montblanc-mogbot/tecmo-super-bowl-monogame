using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Tecmo-style pass rush AI.
///
/// Responsibilities:
/// - Translate a rusher's <see cref="RushComponent"/> gap/contain assignment into a per-tick
///   <see cref="BehaviorComponent.TargetPosition"/>.
/// - Track blocker engagement and periodically attempt deterministic rush moves to disengage.
/// - Apply simple stunt/twist gap swapping based on a frame delay.
///
/// This system is intentionally conservative (scaffold) and aims to be deterministic:
/// - Frame index derived from <see cref="PlayState.PlayElapsedSeconds"/> at 60Hz.
/// - Rush move rolls use a small hash-based RNG seeded by playId + ids + frame.
/// </summary>
public sealed class RushSystem : EntityUpdateSystem
{
    private readonly GameEvents _events;
    private readonly MatchState _match;
    private readonly PlayState _play;

    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<BehaviorComponent> _behavior = null!;
    private ComponentMapper<BehaviorStackComponent> _stack = null!;
    private ComponentMapper<PlayerAttributesComponent> _attrs = null!;
    private ComponentMapper<DefensiveAssignmentComponent> _defAssign = null!;
    private ComponentMapper<RushComponent> _rush = null!;

    public RushSystem(GameEvents events, MatchState match, PlayState play)
        : base(Aspect.All(
            typeof(PositionComponent),
            typeof(BehaviorComponent),
            typeof(BehaviorStackComponent),
            typeof(PlayerAttributesComponent),
            typeof(DefensiveAssignmentComponent),
            typeof(RushComponent)))
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _match = match ?? throw new ArgumentNullException(nameof(match));
        _play = play ?? throw new ArgumentNullException(nameof(play));
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _pos = mapperService.GetMapper<PositionComponent>();
        _behavior = mapperService.GetMapper<BehaviorComponent>();
        _stack = mapperService.GetMapper<BehaviorStackComponent>();
        _attrs = mapperService.GetMapper<PlayerAttributesComponent>();
        _defAssign = mapperService.GetMapper<DefensiveAssignmentComponent>();
        _rush = mapperService.GetMapper<RushComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        // Only meaningful during live play.
        if (_play.Phase != PlayPhase.InPlay || _play.IsOver)
            return;

        var frame = ToFrameIndex60Hz(_play.PlayElapsedSeconds);

        // Consume block contact events to mark engaged state for rushers.
        _events.Drain<BlockContactEvent>(e =>
        {
            if (!_rush.Has(e.DefenderId))
                return;

            var r = _rush.Get(e.DefenderId);
            r.Engaged = true;
            r.EngagedBlockerId = e.BlockerId;
        });

        foreach (var entityId in ActiveEntities)
        {
            var beh = _behavior.Get(entityId);
            var da = _defAssign.Get(entityId);
            var r = _rush.Get(entityId);

            // Only apply to true rushers.
            if (da.Kind != DefensiveAssignmentKind.PassRush)
                continue;

            // Ensure the entity is in a rushing behavior state unless currently interrupt-engaged.
            if (beh.State != BehaviorState.Engaged && beh.State != BehaviorState.RushingQB)
                beh.State = BehaviorState.RushingQB;

            // Stunt/twist: swap gap after delay.
            if (r.IsStunt && frame >= r.StuntDelayFrames)
            {
                r.TargetGap = r.StuntTargetGap;
                r.IsStunt = false;
                r.GapReached = false;
            }

            // Resolve QB target id (assignment target).
            var qbId = da.TargetEntityId;
            if (qbId == -1 || !_pos.Has(qbId))
                continue;

            var qbPos = _pos.Get(qbId).Position;

            // Determine gap landmark (recomputed each tick; deterministic in terms of state inputs).
            r.GapPosition = ComputeGapPosition(qbPos, r.TargetGap);

            // If engaged, attempt rush move on cooldown.
            if (r.Engaged && r.EngagedBlockerId != -1 && beh.State == BehaviorState.Engaged)
            {
                TryResolveRushMove(entityId, frame, ref r);
                continue; // while engaged, don't retarget
            }

            // If we were engaged but are no longer in engaged state (timer expired), clear engaged flag.
            if (beh.State != BehaviorState.Engaged && r.Engaged)
            {
                r.Engaged = false;
                r.EngagedBlockerId = -1;
            }

            // Phase 1: attack gap landmark.
            if (!r.GapReached)
            {
                var p = _pos.Get(entityId).Position;
                if (Vector2.DistanceSquared(p, r.GapPosition) <= 4f * 4f)
                    r.GapReached = true;

                beh.TargetPosition = r.GapPosition;
                continue;
            }

            // Phase 2: rush QB.
            beh.TargetPosition = ComputeQBRushTarget(qbPos, r, entityId);
        }
    }

    private void TryResolveRushMove(int rusherId, int frame, ref RushComponent rush)
    {
        if (frame - rush.LastRushMoveFrame < RushComponent.RUSH_MOVE_COOLDOWN)
            return;

        var blockerId = rush.EngagedBlockerId;
        if (!_attrs.Has(rusherId) || !_attrs.Has(blockerId))
        {
            // If ratings are missing, fail safe: don't auto-win.
            rush.LastRushMoveFrame = frame;
            return;
        }

        var rAttr = _attrs.Get(rusherId);
        var bAttr = _attrs.Get(blockerId);

        // Determine attempt type.
        var attempt = rush.Type;

        // Spin is rare: only attempt if an additional gate passes.
        if (attempt == RushType.Spin)
        {
            var gate = DeterministicFloat01((uint)_play.PlayId, (uint)rusherId, (uint)blockerId, (uint)frame, 0x00511F7u);
            if (gate > 0.15f)
            {
                // No attempt this window; keep cooldown moving so it stays rare.
                rush.LastRushMoveFrame = frame;
                return;
            }
        }

        var pSuccess = ComputeRushMoveSuccessChance(attempt, rAttr, bAttr);
        var u = DeterministicFloat01((uint)_play.PlayId, (uint)rusherId, (uint)blockerId, (uint)frame, 0x0BADC0DEu);

        rush.LastRushMoveFrame = frame;

        if (u < pSuccess)
        {
            // Break the block: clear engagement and immediately pop the engagement interrupt if active.
            rush.Engaged = false;
            rush.EngagedBlockerId = -1;

            var beh = _behavior.Get(rusherId);
            var stack = _stack.Get(rusherId);
            if (stack.TryPeek(out var top) && top.Kind == BehaviorInterruptKind.Engagement)
            {
                stack.TryPop(out var popped);
                BehaviorInterrupt.Restore(beh, popped.Saved);
            }

            // Ensure we go back to rushing.
            beh.State = BehaviorState.RushingQB;
        }
        // else: remain engaged until timer expires; EngagementSystem + BehaviorStackSystem handle the hold.
    }

    private static float ComputeRushMoveSuccessChance(RushType type, PlayerAttributesComponent r, PlayerAttributesComponent b)
    {
        // Spec formula:
        //   Power: (RusherHP - BlockerHP + 25) / 100
        //   Swim:  (RusherMS - BlockerMS + 25) / 100
        // Clamp 10-90%.
        var raw = type switch
        {
            RushType.Power or RushType.Bull => (r.Hp - b.Hp + 25) / 100f,
            RushType.Swim or RushType.Spin => (r.Ms - b.Ms + 25) / 100f,
            _ => 0.25f,
        };

        return Math.Clamp(raw, 0.10f, 0.90f);
    }

    private Vector2 ComputeQBRushTarget(Vector2 qbPos, RushComponent rush, int rusherId)
    {
        // Base target is QB pocket position (slightly behind QB relative to offense direction).
        var sign = _match.OffenseDirection == OffenseDirection.LeftToRight ? -1f : 1f;
        var target = qbPos + new Vector2(6f * sign, 0f);

        if (!rush.IsContain)
            return target;

        // Contain rules (scaffold):
        // - Keep outside leverage (stay outside the tackle box)
        // - Don't get too far upfield (avoid overshooting the QB depth)
        //
        // Without full OL geometry, approximate "tackle box" as +/- 18px from QB Y.
        const float tackleBoxHalfHeight = 18f;

        var myPos = _pos.Get(rusherId).Position;

        // Preserve which side we're on.
        var side = myPos.Y < qbPos.Y ? -1f : 1f;

        // If already outside the box, keep it; otherwise push outside.
        var desiredY = qbPos.Y + side * Math.Max(tackleBoxHalfHeight, Math.Abs(myPos.Y - qbPos.Y));
        target.Y = desiredY;

        // Don't get too far past the QB in X (keep depth).
        // Defense is rushing "into" the pocket, so clamp to near QB X.
        var maxPast = qbPos.X + (10f * -sign); // -sign because sign is "behind offense".
        if (sign < 0f)
        {
            // Offense L->R, defense approaches from right. Prevent contain from going too far left.
            target.X = Math.Max(target.X, maxPast);
        }
        else
        {
            // Offense R->L, defense approaches from left.
            target.X = Math.Min(target.X, maxPast);
        }

        return target;
    }

    private Vector2 ComputeGapPosition(Vector2 qbPos, RushGap gap)
    {
        // Use QB as a stable reference for the pocket center.
        // X is slightly in front of QB (toward the line) so rushers don't target behind him.
        var sign = _match.OffenseDirection == OffenseDirection.LeftToRight ? 1f : -1f; // toward defense
        var x = qbPos.X + (10f * sign);
        var y = qbPos.Y;

        // Y landmarks approximate OL spacing.
        return gap switch
        {
            RushGap.ALeft => new Vector2(x, y - 4f),
            RushGap.ARight => new Vector2(x, y + 4f),

            RushGap.BLeft => new Vector2(x, y - 10f),
            RushGap.BRight => new Vector2(x, y + 10f),

            RushGap.CLeft => new Vector2(x, y - 18f),
            RushGap.CRight => new Vector2(x, y + 18f),

            RushGap.ContainLeft => new Vector2(x + (6f * sign), y - 28f),
            RushGap.ContainRight => new Vector2(x + (6f * sign), y + 28f),

            _ => new Vector2(x, y),
        };
    }

    private static int ToFrameIndex60Hz(float seconds)
    {
        if (seconds <= 0f)
            return 0;

        // Floor is deterministic and matches "frames elapsed" semantics.
        return (int)MathF.Floor(seconds * 60f);
    }

    private static float DeterministicFloat01(uint playId, uint a, uint b, uint frame, uint salt)
    {
        // Tiny hash/xorshift -> [0,1). Deterministic across platforms.
        unchecked
        {
            uint x = 0x9E3779B9u;
            x ^= playId + 0x85EBCA6Bu;
            x ^= a * 0xC2B2AE35u;
            x ^= b * 0x27D4EB2Fu;
            x ^= frame * 0x165667B1u;
            x ^= salt;

            // xorshift
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;

            // Convert to [0,1)
            return (x & 0x00FFFFFFu) / 16777216f;
        }
    }
}
