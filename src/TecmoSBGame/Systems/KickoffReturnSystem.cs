using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Kickoff return lane selection (find seam, follow blockers) — scaffold.
///
/// Behavior:
/// - Active only during kickoff slice (PlayState.AllowPass == false) and live play.
/// - If returner has ball and isn't player-controlled, choose a lane (L/C/R) based on
///   the number of coverage defenders "ahead" within a look-ahead box.
/// - Lane choice is sticky for a short window (LaneLockFrames) to avoid oscillation.
/// </summary>
public sealed class KickoffReturnSystem : EntityUpdateSystem
{
    private readonly MatchState _match;
    private readonly PlayState _play;

    private ComponentMapper<KickoffReturnComponent> _kr = null!;
    private ComponentMapper<BallCarrierComponent> _carrier = null!;
    private ComponentMapper<TeamComponent> _team = null!;
    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<BehaviorComponent> _behavior = null!;
    private ComponentMapper<PlayerControlComponent> _control = null!;

    public KickoffReturnSystem(MatchState match, PlayState play)
        : base(Aspect.All(typeof(KickoffReturnComponent), typeof(BallCarrierComponent), typeof(TeamComponent), typeof(PositionComponent), typeof(BehaviorComponent)))
    {
        _match = match ?? throw new ArgumentNullException(nameof(match));
        _play = play ?? throw new ArgumentNullException(nameof(play));
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _kr = mapperService.GetMapper<KickoffReturnComponent>();
        _carrier = mapperService.GetMapper<BallCarrierComponent>();
        _team = mapperService.GetMapper<TeamComponent>();
        _pos = mapperService.GetMapper<PositionComponent>();
        _behavior = mapperService.GetMapper<BehaviorComponent>();
        _control = mapperService.GetMapper<PlayerControlComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (_play.Phase != PlayPhase.InPlay || _play.IsOver)
            return;

        // Kickoff slice only.
        if (_play.AllowPass)
            return;

        // Derive a stable 60Hz frame index for lock timing.
        var frame = (int)MathF.Floor(_play.PlayElapsedSeconds * 60f);

        foreach (var id in ActiveEntities)
        {
            if (!_carrier.Get(id).HasBall)
                continue;

            // If player is controlling, don't steer.
            if (_control.Has(id) && _control.Get(id).IsControlled)
                continue;

            var r = _kr.Get(id);
            var myPos = _pos.Get(id).Position;

            if (r.LaneLockFrames > 0)
            {
                r.LaneLockFrames = Math.Max(0, r.LaneLockFrames - 1);
                SetTarget(id, r.LastChosenTarget);
                continue;
            }

            var dir = _match.OffenseDirection;
            var signX = dir == OffenseDirection.LeftToRight ? 1f : -1f;

            // Candidate lane targets: same x look-ahead, different y offsets.
            var lookAheadX = myPos.X + 44f * signX;
            var left = new Vector2(lookAheadX, myPos.Y - 28f);
            var center = new Vector2(lookAheadX, myPos.Y);
            var right = new Vector2(lookAheadX, myPos.Y + 28f);

            var sLeft = ScoreLane(id, myPos, left, signX);
            var sCenter = ScoreLane(id, myPos, center, signX);
            var sRight = ScoreLane(id, myPos, right, signX);

            var (lane, target) = Min3(sLeft, left, sCenter, center, sRight, right);

            r.Lane = lane;
            r.LastChosenTarget = target;
            r.LaneLockFrames = 20; // ~0.33s

            SetTarget(id, target);
        }
    }

    private void SetTarget(int id, Vector2 target)
    {
        var b = _behavior.Get(id);
        b.State = BehaviorState.MovingToPosition;
        b.TargetPosition = ClampToField(target);
    }

    private Vector2 ClampToField(Vector2 p)
    {
        const float left = 16f;
        const float right = 240f;
        const float top = 40f;
        const float bottom = 184f;

        return new Vector2(
            MathHelper.Clamp(p.X, left, right),
            MathHelper.Clamp(p.Y, top, bottom));
    }

    private int ScoreLane(int returnerId, Vector2 returnerPos, Vector2 laneTarget, float signX)
    {
        // Lower score is better.
        // Count coverage defenders in a look-ahead rectangle centered on laneTarget.
        // Deterministic and cheap (entity count is small).
        var score = 0;

        var box = RectFromCenter(laneTarget, width: 70f, height: 44f);

        foreach (var otherId in ActiveEntities)
        {
            if (otherId == returnerId)
                continue;
            if (!_team.Has(otherId) || !_pos.Has(otherId))
                continue;

            var t = _team.Get(otherId);
            if (t.TeamIndex == _team.Get(returnerId).TeamIndex)
                continue;

            // Only treat defense (coverage team) as threats.
            if (t.IsOffense)
                continue;

            var p = _pos.Get(otherId).Position;
            if (!box.Contains(p))
                continue;

            // Bias toward defenders that are "ahead" (in front of returner in X).
            var ahead = (p.X - returnerPos.X) * signX > -2f;
            score += ahead ? 2 : 1;
        }

        return score;
    }

    private static (KickoffReturnLane lane, Vector2 target) Min3(int a, Vector2 at, int b, Vector2 bt, int c, Vector2 ct)
    {
        if (a <= b && a <= c) return (KickoffReturnLane.Left, at);
        if (b <= a && b <= c) return (KickoffReturnLane.Center, bt);
        return (KickoffReturnLane.Right, ct);
    }

    private readonly record struct Rect(float Left, float Top, float Right, float Bottom)
    {
        public static Rect FromCenter(Vector2 c, float width, float height)
        {
            var hw = width * 0.5f;
            var hh = height * 0.5f;
            return new Rect(c.X - hw, c.Y - hh, c.X + hw, c.Y + hh);
        }

        public bool Contains(Vector2 p) => p.X >= Left && p.X <= Right && p.Y >= Top && p.Y <= Bottom;
    }

    private static Rect RectFromCenter(Vector2 c, float width, float height) => Rect.FromCenter(c, width, height);
}
