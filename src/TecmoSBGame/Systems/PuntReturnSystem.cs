using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Punt return lane selection (set up wall, find lane) — scaffold.
///
/// Active during non-pass special-teams slices; if a punt returner has the ball and isn't
/// player-controlled, choose a seam to attack based on nearby coverage defenders.
/// </summary>
public sealed class PuntReturnSystem : EntityUpdateSystem
{
    private readonly MatchState _match;
    private readonly PlayState _play;

    private ComponentMapper<PuntReturnComponent> _pr = null!;
    private ComponentMapper<BallCarrierComponent> _carrier = null!;
    private ComponentMapper<TeamComponent> _team = null!;
    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<BehaviorComponent> _behavior = null!;
    private ComponentMapper<PlayerControlComponent> _control = null!;

    public PuntReturnSystem(MatchState match, PlayState play)
        : base(Aspect.All(typeof(PuntReturnComponent), typeof(BallCarrierComponent), typeof(TeamComponent), typeof(PositionComponent), typeof(BehaviorComponent)))
    {
        _match = match ?? throw new ArgumentNullException(nameof(match));
        _play = play ?? throw new ArgumentNullException(nameof(play));
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _pr = mapperService.GetMapper<PuntReturnComponent>();
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

        // Special-teams slice only (no passing).
        if (_play.AllowPass)
            return;

        foreach (var id in ActiveEntities)
        {
            if (!_carrier.Get(id).HasBall)
                continue;

            // Return team is offense.
            if (!_team.Get(id).IsOffense)
                continue;

            if (_control.Has(id) && _control.Get(id).IsControlled)
                continue;

            var r = _pr.Get(id);
            var myPos = _pos.Get(id).Position;

            if (r.LaneLockFrames > 0)
            {
                r.LaneLockFrames--;
                Steer(id, r.LastChosenTarget);
                continue;
            }

            var dir = _match.OffenseDirection;
            var signX = dir == OffenseDirection.LeftToRight ? 1f : -1f;

            // Punt returns tend to set up a wall; keep targets a bit more lateral.
            var lookAheadX = myPos.X + 36f * signX;
            var left = new Vector2(lookAheadX, myPos.Y - 34f);
            var center = new Vector2(lookAheadX, myPos.Y);
            var right = new Vector2(lookAheadX, myPos.Y + 34f);

            var sLeft = ScoreLane(id, myPos, left, signX);
            var sCenter = ScoreLane(id, myPos, center, signX);
            var sRight = ScoreLane(id, myPos, right, signX);

            var (lane, target) = Min3(sLeft, left, sCenter, center, sRight, right);
            r.Lane = lane;
            r.LastChosenTarget = target;
            r.LaneLockFrames = 24;

            Steer(id, target);
        }
    }

    private void Steer(int id, Vector2 target)
    {
        var b = _behavior.Get(id);
        b.State = BehaviorState.MovingToPosition;
        b.TargetPosition = ClampToField(target);
    }

    private static Vector2 ClampToField(Vector2 p)
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
        // Lower is better.
        var score = 0;
        var rect = Rect.FromCenter(laneTarget, width: 74f, height: 52f);

        var myTeam = _team.Get(returnerId).TeamIndex;

        foreach (var otherId in ActiveEntities)
        {
            if (otherId == returnerId)
                continue;
            if (!_team.Has(otherId) || !_pos.Has(otherId))
                continue;

            var t = _team.Get(otherId);
            if (t.TeamIndex == myTeam)
                continue;

            // Coverage team is defense.
            if (t.IsOffense)
                continue;

            var p = _pos.Get(otherId).Position;
            if (!rect.Contains(p))
                continue;

            var ahead = (p.X - returnerPos.X) * signX > -2f;
            score += ahead ? 2 : 1;
        }

        return score;
    }

    private static (PuntReturnLane lane, Vector2 target) Min3(int a, Vector2 at, int b, Vector2 bt, int c, Vector2 ct)
    {
        if (a <= b && a <= c) return (PuntReturnLane.Left, at);
        if (b <= a && b <= c) return (PuntReturnLane.Center, bt);
        return (PuntReturnLane.Right, ct);
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
}
