using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Field;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Deterministic pre-snap placement for player entities.
///
/// Aligns the current formation to the line of scrimmage derived from
/// <see cref="MatchState.BallSpot"/>.
///
/// Implementation detail:
/// We use a best-effort "center" reference player (offense OL slot OC/C) as the anchor.
/// This avoids depending on a specific spawner anchor (kickoff/hike/mid) while the
/// formation YAML remains incomplete.
/// </summary>
public sealed class PreSnapSystem : EntityUpdateSystem
{
    private readonly LoopState? _loop;
    private readonly MatchState? _match;
    private readonly PlayState? _play;

    private ComponentMapper<PositionComponent> _pos;
    private ComponentMapper<TeamComponent> _team;
    private ComponentMapper<PlayerRoleComponent> _role;
    private ComponentMapper<PlayerAttributesComponent> _attr;
    private ComponentMapper<BallCarrierComponent> _carrier;

    public PreSnapSystem(LoopState? loop = null, MatchState? matchState = null, PlayState? playState = null)
        : base(Aspect.All(typeof(PositionComponent), typeof(TeamComponent)))
    {
        _loop = loop;
        _match = matchState;
        _play = playState;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _pos = mapperService.GetMapper<PositionComponent>();
        _team = mapperService.GetMapper<TeamComponent>();
        _role = mapperService.GetMapper<PlayerRoleComponent>();
        _attr = mapperService.GetMapper<PlayerAttributesComponent>();
        _carrier = mapperService.GetMapper<BallCarrierComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (_loop is not null && !_loop.IsOnField("pre_snap"))
            return;

        // Keep kickoff slice unchanged: kickoff pre-snap uses BallState.Held.
        if (_play is not null)
        {
            if (_play.Phase != PlayPhase.PreSnap)
                return;

            if (_play.BallState != BallState.Dead)
                return;
        }

        if (_match is null)
            return;

        var dirSign = _match.OffenseDirection == OffenseDirection.LeftToRight ? 1f : -1f;

        var losAbsYard = PlayState.ToAbsoluteYard(_match.BallSpot, _match.OffenseDirection);
        var losX = AbsoluteYardToX(losAbsYard);

        // Reference X: offense center (OC/C) when found; otherwise first offensive entity.
        if (!TryFindOffenseAnchorX(out var anchorX))
            return;

        // Rough 1-yard cushion for defenders.
        var yardPixels = (FieldBounds.FieldRightX - FieldBounds.FieldLeftX) / 100f;
        var defenseSeparation = dirSign * yardPixels * 1.0f;

        foreach (var entityId in ActiveEntities)
        {
            var t = _team.Get(entityId);
            var p = _pos.Get(entityId);

            var localX = p.Position.X - anchorX;
            var x = losX + localX * dirSign;

            if (!t.IsOffense)
                x += defenseSeparation;

            p.Position = new Vector2(x, p.Position.Y);

            // Pre-snap: ensure no player claims possession.
            if (_carrier.Has(entityId))
                _carrier.Get(entityId).HasBall = false;
        }

        // Ball placement is handled by PreSnapBallPlacementSystem.

        // TODO(pre-snap): optional deterministic motion hook.
    }

    private bool TryFindOffenseAnchorX(out float anchorX)
    {
        anchorX = 0f;

        // Prefer an offensive center.
        var haveFallback = false;
        var fallbackX = 0f;

        foreach (var id in ActiveEntities)
        {
            var t = _team.Get(id);
            if (!t.IsOffense)
                continue;

            var x = _pos.Get(id).Position.X;
            if (!haveFallback)
            {
                haveFallback = true;
                fallbackX = x;
            }

            if (!IsCenterSlot(id))
                continue;

            anchorX = x;
            return true;
        }

        if (haveFallback)
        {
            anchorX = fallbackX;
            return true;
        }

        return false;
    }

    private bool IsCenterSlot(int entityId)
    {
        // PlayerAttributes.Position can contain raw slot strings (OC/LG/etc).
        if (_attr.Has(entityId))
        {
            var pos = (_attr.Get(entityId).Position ?? string.Empty).Trim().ToUpperInvariant();
            if (pos == "C" || pos.Contains("OC"))
                return true;
        }

        if (_role.Has(entityId))
        {
            var slot = (_role.Get(entityId).Slot ?? string.Empty).Trim().ToUpperInvariant();
            if (slot == "C" || slot.Contains("OC"))
                return true;

            // Some YAML uses just "C" (or may omit OC).
        }

        return false;
    }

    private static float AbsoluteYardToX(int absoluteYard0To100)
    {
        absoluteYard0To100 = Math.Clamp(absoluteYard0To100, 0, 100);
        var t = absoluteYard0To100 / 100f;
        return FieldBounds.FieldLeftX + t * (FieldBounds.FieldRightX - FieldBounds.FieldLeftX);
    }
}
