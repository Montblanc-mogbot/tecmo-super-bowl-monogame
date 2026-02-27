using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Resolves a snap into play/ball state transitions.
///
/// Upstream systems publish <see cref="SnapEvent"/> (typically via the action command layer).
/// This system consumes the event and:
/// - transitions <see cref="PlayState.Phase"/> to <see cref="PlayPhase.InPlay"/>
/// - grants possession to the offense QB (best-effort)
/// - syncs the dedicated ball entity to a Held state
/// </summary>
public sealed class SnapResolutionSystem : EntityUpdateSystem
{
    private readonly GameEvents? _events;
    private readonly MatchState? _match;
    private readonly PlayState? _play;

    private ComponentMapper<TeamComponent> _team;
    private ComponentMapper<PlayerRoleComponent> _role;
    private ComponentMapper<PositionComponent> _pos;
    private ComponentMapper<BallCarrierComponent> _carrier;

    private ComponentMapper<BallStateComponent> _ballState;
    private ComponentMapper<BallOwnerComponent> _ballOwner;

    public SnapResolutionSystem(GameEvents? events = null, MatchState? matchState = null, PlayState? playState = null)
        : base(Aspect.All(typeof(PositionComponent)))
    {
        _events = events;
        _match = matchState;
        _play = playState;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _team = mapperService.GetMapper<TeamComponent>();
        _role = mapperService.GetMapper<PlayerRoleComponent>();
        _pos = mapperService.GetMapper<PositionComponent>();
        _carrier = mapperService.GetMapper<BallCarrierComponent>();

        _ballState = mapperService.GetMapper<BallStateComponent>();
        _ballOwner = mapperService.GetMapper<BallOwnerComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (_events is null || _play is null)
            return;

        if (_play.Phase != PlayPhase.PreSnap)
            return;

        var snaps = _events.Read<SnapEvent>();
        if (snaps.Count <= 0)
            return;

        // Identify offense/defense teams.
        var offenseTeam = _match?.PossessionTeam ?? 0;

        // Find an offense QB to receive the ball.
        int? qbId = null;
        foreach (var id in ActiveEntities)
        {
            if (!_team.Has(id) || !_role.Has(id))
                continue;

            var t = _team.Get(id);
            if (!t.IsOffense || t.TeamIndex != offenseTeam)
                continue;

            if (_role.Get(id).Role == PlayerRole.QB)
            {
                qbId = id;
                break;
            }
        }

        // Transition play model.
        _play.Phase = PlayPhase.InPlay;
        _play.PlayElapsedSeconds = 0f;

        if (qbId is not null)
        {
            _play.BallState = BallState.Held;
            _play.BallOwnerEntityId = qbId.Value;

            // Update player has-ball flags.
            foreach (var id in ActiveEntities)
            {
                if (_carrier.Has(id))
                    _carrier.Get(id).HasBall = (id == qbId.Value);
            }

            // Sync the dedicated ball entity if present in this world.
            // We do a best-effort scan for an entity with BallState/BallOwner; this is cheap at 22 entities.
            var qbPos = _pos.Has(qbId.Value) ? _pos.Get(qbId.Value).Position : Vector2.Zero;
            foreach (var id in ActiveEntities)
            {
                // ActiveEntities is position-based; ball has Position too so it participates.
                if (!_ballState.Has(id) || !_ballOwner.Has(id))
                    continue;

                _ballState.Get(id).State = BallState.Held;
                _ballOwner.Get(id).OwnerEntityId = qbId.Value;
                _pos.Get(id).Position = qbPos;
                break;
            }
        }
        else
        {
            // No QB: still transition the play; ball remains dead/ownerless.
            _play.BallState = BallState.Dead;
            _play.BallOwnerEntityId = null;
        }
    }
}
