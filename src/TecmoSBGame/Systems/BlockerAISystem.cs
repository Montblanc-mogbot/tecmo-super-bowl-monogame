using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Assignment-based blocking AI (Tecmo-style):
/// - On snap, pick a target defender based on assignment/lane.
/// - Move toward that defender (not the ball carrier).
/// - Engagement is "sticky" via EngagementSystem; this system drives re-targeting/releasing.
/// - Detect double-teams and apply a heavy defender speed penalty.
/// - Optional cut blocks (deterministic random) as a short-lived pancake effect.
/// - Optional second-level release after a completed engagement window.
/// </summary>
public sealed class BlockerAISystem : EntityUpdateSystem
{
    public const float CONTACT_DISTANCE_PIXELS = 6f; // Assembly notes suggest ~4-6px.

    // If engaged and then released, after this many frames we can seek second level.
    private const int DEFAULT_RELEASE_TO_SECOND_LEVEL_AFTER_FRAMES = 28; // ~0.47s @ 60Hz.

    private const float DOUBLE_TEAM_DEFENDER_SPEED_MULT = 0.45f;
    private const float DOUBLE_TEAM_REFRESH_SECONDS = 0.12f;

    private const float CUT_BLOCK_DEFENDER_SPEED_MULT = 0.25f;
    private const float CUT_BLOCK_DEFENDER_TIMER_SECONDS = 0.55f;

    private readonly GameEvents _events;
    private readonly LoopState? _loop;
    private readonly PlayState? _play;

    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<VelocityComponent> _vel = null!;
    private ComponentMapper<TeamComponent> _team = null!;
    private ComponentMapper<PlayerRoleComponent> _role = null!;
    private ComponentMapper<BehaviorComponent> _behavior = null!;
    private ComponentMapper<BlockTargetComponent> _blockTarget = null!;
    private ComponentMapper<BallCarrierComponent> _ballCarrier = null!;
    private ComponentMapper<SpeedModifierComponent> _speedMod = null!;

    private int _lastSnapTick = -1;

    public BlockerAISystem(GameEvents events, LoopState? loop = null, PlayState? play = null)
        : base(Aspect.All(typeof(BlockTargetComponent), typeof(PositionComponent), typeof(TeamComponent), typeof(BehaviorComponent)))
    {
        _events = events;
        _loop = loop;
        _play = play;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _pos = mapperService.GetMapper<PositionComponent>();
        _vel = mapperService.GetMapper<VelocityComponent>();
        _team = mapperService.GetMapper<TeamComponent>();
        _role = mapperService.GetMapper<PlayerRoleComponent>();
        _behavior = mapperService.GetMapper<BehaviorComponent>();
        _blockTarget = mapperService.GetMapper<BlockTargetComponent>();
        _ballCarrier = mapperService.GetMapper<BallCarrierComponent>();
        _speedMod = mapperService.GetMapper<SpeedModifierComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        // Only drive blocking during live play.
        if (_loop is not null && !_loop.IsOnField("live_play"))
            return;

        ConsumeSnapEvents();

        // Build a stable list of defender entities and ball carrier.
        var defenders = GatherDefenders(out var ballCarrierId);

        // First pass: for each blocker, ensure it has a target and drive movement.
        foreach (var blockerId in ActiveEntities.OrderBy(i => i))
        {
            var t = _team.Get(blockerId);
            if (!t.IsOffense)
                continue;

            // Skip ball carrier (should not block).
            if (ballCarrierId == blockerId)
                continue;

            var bt = _blockTarget.Get(blockerId);
            var behavior = _behavior.Get(blockerId);

            // Keep engagement bookkeeping in sync.
            SyncEngagementFlags(blockerId, bt, behavior);

            // If engaged, do not steer via TargetPosition (EngagementSystem interrupts movement).
            if (bt.IsEngaged)
                continue;

            // After a completed engagement window, optionally release to second level.
            if (bt.EngagementFrame >= DEFAULT_RELEASE_TO_SECOND_LEVEL_AFTER_FRAMES)
            {
                bt.Assignment = BlockAssignmentType.SecondLevel;
                bt.TargetEntityId = -1;
            }

            if (bt.TargetEntityId == -1 || !defenders.Contains(bt.TargetEntityId))
                bt.TargetEntityId = ChooseTargetDefender(blockerId, bt.Assignment, defenders);

            if (bt.TargetEntityId == -1)
            {
                // Nothing to do this tick.
                behavior.State = BehaviorState.Idle;
                continue;
            }

            // Drive movement toward the target.
            var targetPos = _pos.Get(bt.TargetEntityId).Position;
            behavior.State = BehaviorState.MovingToPosition;
            behavior.TargetEntityId = bt.TargetEntityId;
            behavior.TargetPosition = targetPos;

            // Optional cut block attempt when closing in.
            MaybeAttemptCutBlock(blockerId, bt, targetPos);
        }

        // Second pass: detect double-teams among currently engaged blockers.
        ApplyDoubleTeams();
    }

    private void ConsumeSnapEvents()
    {
        // On snap, clear targets/engagement and re-select deterministically.
        // (We use the on-field loop tick as a stable per-snap identifier when available.)
        var snapEvents = _events.Read<SnapEvent>();
        if (snapEvents.Count == 0)
            return;

        // Deduplicate within a tick.
        var snapTick = _loop?.OnFieldTick ?? 0;
        if (snapTick == _lastSnapTick)
            return;

        _lastSnapTick = snapTick;

        foreach (var blockerId in ActiveEntities)
        {
            var t = _team.Get(blockerId);
            if (!t.IsOffense)
                continue;

            var bt = _blockTarget.Get(blockerId);
            bt.TargetEntityId = -1;
            bt.IsEngaged = false;
            bt.EngagedEntityId = -1;
            bt.EngagementFrame = 0;
            bt.IsDoubleTeam = false;
        }
    }

    private HashSet<int> GatherDefenders(out int ballCarrierId)
    {
        ballCarrierId = -1;

        var defenders = new HashSet<int>();
        foreach (var id in ActiveEntities)
        {
            if (_ballCarrier.Has(id) && _ballCarrier.Get(id).HasBall)
                ballCarrierId = id;

            if (!_team.Has(id))
                continue;

            var t = _team.Get(id);
            if (!t.IsOffense)
                defenders.Add(id);
        }

        return defenders;
    }

    private void SyncEngagementFlags(int blockerId, BlockTargetComponent bt, BehaviorComponent behavior)
    {
        // EngagementSystem sets BehaviorState.Engaged and targets partner id.
        if (behavior.State == BehaviorState.Engaged)
        {
            if (!bt.IsEngaged)
            {
                bt.IsEngaged = true;
                bt.EngagedEntityId = behavior.TargetEntityId;
                bt.EngagementFrame = 0;
            }
            else
            {
                bt.EngagementFrame++;
                bt.EngagedEntityId = behavior.TargetEntityId;
            }

            return;
        }

        // Leaving engaged state.
        if (bt.IsEngaged)
        {
            bt.IsEngaged = false;
            bt.EngagedEntityId = -1;
            bt.IsDoubleTeam = false;
            // Keep EngagementFrame as a "recent engagement" duration to gate second-level release.
        }
    }

    private int ChooseTargetDefender(int blockerId, BlockAssignmentType assignment, HashSet<int> defenders)
    {
        if (defenders.Count == 0)
            return -1;

        var blockerPos = _pos.Get(blockerId).Position;

        // Lane preference based on blocker slot (LG/LT left, RG/RT right, C middle).
        float laneBiasY = 0f;
        if (_role.Has(blockerId))
        {
            var slot = (_role.Get(blockerId).Slot ?? string.Empty).ToUpperInvariant();
            if (slot.Contains("LG") || slot.Contains("LT")) laneBiasY = -12f;
            else if (slot.Contains("RG") || slot.Contains("RT")) laneBiasY = +12f;
            else if (slot.Contains("OC") || slot == "C") laneBiasY = 0f;
        }

        // Desired search center for gap/pull/second-level.
        var desired = blockerPos;
        desired.Y += assignment switch
        {
            BlockAssignmentType.GapLeft => -10f,
            BlockAssignmentType.GapRight => +10f,
            BlockAssignmentType.PullLeft => -18f,
            BlockAssignmentType.PullRight => +18f,
            BlockAssignmentType.SecondLevel => laneBiasY * 1.2f,
            _ => laneBiasY,
        };

        // Prefer defenders "ahead" of the blocker along the offense's direction.
        // We approximate by using current velocity/behavior direction as facing.
        var facing = GetFacingDirection(blockerId);

        var bestId = -1;
        var bestScore = float.PositiveInfinity;

        foreach (var defId in defenders.OrderBy(i => i))
        {
            // Avoid ball carrier if it was mis-tagged.
            if (defId == blockerId)
                continue;

            var defPos = _pos.Get(defId).Position;
            var toDef = defPos - blockerPos;

            // Don't pick defenders behind us (Tecmo: can't block behind).
            if (facing != Vector2.Zero && Vector2.Dot(Vector2.Normalize(toDef), facing) < -0.10f)
                continue;

            // Second level prefers LBs/DBs.
            if (assignment == BlockAssignmentType.SecondLevel && _role.Has(defId))
            {
                var r = _role.Get(defId).Role;
                if (r == PlayerRole.DL)
                    continue;
            }

            // Score: distance to desired point + small role weight + deterministic tie-break.
            var dx = defPos.X - desired.X;
            var dy = defPos.Y - desired.Y;
            var dist = (dx * dx) + (dy * dy);

            // Prefer DL for initial blocks.
            var rolePenalty = 0f;
            if (_role.Has(defId))
            {
                var r = _role.Get(defId).Role;
                rolePenalty = assignment == BlockAssignmentType.SecondLevel
                    ? (r == PlayerRole.LB ? 0f : 12f)
                    : (r == PlayerRole.DL ? 0f : 18f);
            }

            var score = dist + (rolePenalty * rolePenalty);
            if (score < bestScore)
            {
                bestScore = score;
                bestId = defId;
            }
        }

        return bestId;
    }

    private Vector2 GetFacingDirection(int entityId)
    {
        // Prefer current velocity direction; fall back to behavior target direction.
        if (_vel.Has(entityId))
        {
            var v = _vel.Get(entityId).Velocity;
            if (v.LengthSquared() > 0.01f)
                return Vector2.Normalize(v);
        }

        var b = _behavior.Get(entityId);
        var to = b.TargetPosition - _pos.Get(entityId).Position;
        if (to.LengthSquared() > 1f)
            return Vector2.Normalize(to);

        return Vector2.Zero;
    }

    private void ApplyDoubleTeams()
    {
        // defenderId -> blockers engaged
        var counts = new Dictionary<int, List<int>>(capacity: 8);

        foreach (var blockerId in ActiveEntities)
        {
            var t = _team.Get(blockerId);
            if (!t.IsOffense)
                continue;

            var bt = _blockTarget.Get(blockerId);
            if (!bt.IsEngaged || bt.EngagedEntityId == -1)
                continue;

            if (!counts.TryGetValue(bt.EngagedEntityId, out var list))
            {
                list = new List<int>(capacity: 2);
                counts[bt.EngagedEntityId] = list;
            }
            list.Add(blockerId);
        }

        foreach (var kv in counts)
        {
            var defenderId = kv.Key;
            var blockers = kv.Value;
            var isDouble = blockers.Count >= 2;

            if (!isDouble)
                continue;

            for (var i = 0; i < blockers.Count; i++)
                _blockTarget.Get(blockers[i]).IsDoubleTeam = true;

            // Slow defender significantly while double-teamed.
            EnsureSpeedMod(defenderId, DOUBLE_TEAM_DEFENDER_SPEED_MULT, DOUBLE_TEAM_REFRESH_SECONDS);
        }
    }

    private void MaybeAttemptCutBlock(int blockerId, BlockTargetComponent bt, Vector2 targetPos)
    {
        // Only for initial line blocks (not second-level).
        if (bt.Assignment == BlockAssignmentType.SecondLevel)
            return;

        // Must be close enough and moving toward the defender.
        var pos = _pos.Get(blockerId).Position;
        var distSq = Vector2.DistanceSquared(pos, targetPos);
        if (distSq > (CONTACT_DISTANCE_PIXELS * 1.6f) * (CONTACT_DISTANCE_PIXELS * 1.6f))
            return;

        // Deterministic random gate based on play + entity + snap tick.
        var playId = (uint)(_play?.PlayId ?? 0);
        var snap = (uint)(_lastSnapTick < 0 ? 0 : _lastSnapTick);
        var u = DeterministicFloat01(playId, (uint)blockerId, salt: 0xC0DB10CCu ^ snap);

        // Chance is modest; slightly higher for slower blockers (classic OL) and low RS defenders isn't modeled yet.
        var chance = 0.06f;
        if (_vel.Has(blockerId))
        {
            var max = _vel.Get(blockerId).MaxSpeed;
            if (max <= 3.5f) chance += 0.03f;
        }

        if (u >= chance)
            return;

        // Execute "cut": slow defender strongly (pancake-like) and stop blocker from releasing downfield.
        var defenderId = bt.TargetEntityId;
        if (defenderId == -1)
            return;

        EnsureSpeedMod(defenderId, CUT_BLOCK_DEFENDER_SPEED_MULT, CUT_BLOCK_DEFENDER_TIMER_SECONDS);

        // Cut blocker gives up on second-level: keep assignment as ManOn and let engagement glue happen.
        bt.EngagementFrame = 0;
    }

    private void EnsureSpeedMod(int entityId, float multiplier, float timerSeconds)
    {
        // All player entities are expected to have SpeedModifierComponent via PlayerEntityFactory.
        // If missing, skip rather than trying to attach (systems should not mutate entity composition here).
        if (!_speedMod.Has(entityId))
            return;

        var m = _speedMod.Get(entityId);
        m.MaxSpeedMultiplier = MathF.Min(m.MaxSpeedMultiplier, multiplier);
        m.TimerSeconds = MathF.Max(m.TimerSeconds, timerSeconds);
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
