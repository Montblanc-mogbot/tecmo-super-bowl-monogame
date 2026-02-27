using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Tecmo-style controlled player selection.
///
/// Responsibilities:
/// - Decide which single entity receives movement input.
/// - Allow simple manual switching (Tab) when allowed by loop gating.
///
/// Determinism:
/// - When <paramref name="enableInput"/> is false (headless), selection is purely rule-based.
/// - Tie-breakers are stable (entityId ordering).
/// </summary>
public sealed class PlayerControlSystem : EntityUpdateSystem
{
    private readonly ControlState _control;
    private readonly LoopState? _loop;
    private readonly bool _enableInput;

    private ComponentMapper<TeamComponent> _teamMapper;
    private ComponentMapper<PlayerControlComponent> _controlMapper;
    private ComponentMapper<PositionComponent> _posMapper;
    private ComponentMapper<BallCarrierComponent> _ballMapper;
    private ComponentMapper<PlayerAttributesComponent> _attrMapper;

    public PlayerControlSystem(ControlState control, LoopState? loop = null, bool enableInput = true)
        : base(Aspect.All(typeof(TeamComponent), typeof(PlayerControlComponent), typeof(PositionComponent)))
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _loop = loop;
        _enableInput = enableInput;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _teamMapper = mapperService.GetMapper<TeamComponent>();
        _controlMapper = mapperService.GetMapper<PlayerControlComponent>();
        _posMapper = mapperService.GetMapper<PositionComponent>();
        _ballMapper = mapperService.GetMapper<BallCarrierComponent>();
        _attrMapper = mapperService.GetMapper<PlayerAttributesComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        // Only allow manual switching during on-field pre_snap/live_play.
        var canSwitch = _loop is null || _loop.IsOnField("pre_snap", "live_play");

        var wantsSwitch = false;
        if (_enableInput && canSwitch)
        {
            var tabDown = Keyboard.GetState().IsKeyDown(Keys.Tab);
            wantsSwitch = tabDown && !_control.PrevSwitchDown;
            _control.PrevSwitchDown = tabDown;
        }
        else
        {
            // Keep edge state stable.
            _control.PrevSwitchDown = false;
        }

        var entities = GetDeterministicEntities();
        if (entities.Count == 0)
        {
            _control.SetControlledEntity(null);
            _control.Role = ControlRole.Unknown;
            return;
        }

        var ballCarrierId = FindBallCarrier(entities);
        var playerTeamIndex = ResolvePlayerTeamIndex(entities, ballCarrierId);

        // Manual switch overrides the default selection.
        int? desired = wantsSwitch
            ? ChooseNextOnSwitch(entities, playerTeamIndex, ballCarrierId)
            : ChooseDefault(entities, playerTeamIndex, ballCarrierId);

        ApplySelection(entities, desired, playerTeamIndex, ballCarrierId);
    }

    private List<int> GetDeterministicEntities()
    {
        var list = new List<int>(ActiveEntities.Count);
        foreach (var id in ActiveEntities)
            list.Add(id);
        list.Sort();
        return list;
    }

    private int? FindBallCarrier(List<int> entities)
    {
        // Stable: smallest entityId with HasBall.
        foreach (var id in entities)
        {
            if (_ballMapper.Has(id) && _ballMapper.Get(id).HasBall)
                return id;
        }

        return null;
    }

    private int? ResolvePlayerTeamIndex(List<int> entities, int? ballCarrierId)
    {
        // Prefer any "player-controlled team" entity that currently has the ball.
        if (ballCarrierId is not null)
        {
            var team = _teamMapper.Get(ballCarrierId.Value);
            if (team.IsPlayerControlled)
            {
                _control.ControlledTeamIndex = team.TeamIndex;
                return team.TeamIndex;
            }
        }

        // If we already have a team, keep it (stable across ticks).
        if (_control.ControlledTeamIndex is not null)
            return _control.ControlledTeamIndex;

        // Otherwise choose the first team that is tagged as player-controlled.
        foreach (var id in entities)
        {
            var team = _teamMapper.Get(id);
            if (team.IsPlayerControlled)
            {
                _control.ControlledTeamIndex = team.TeamIndex;
                return team.TeamIndex;
            }
        }

        return null;
    }

    private int? ChooseDefault(List<int> entities, int? playerTeamIndex, int? ballCarrierId)
    {
        // If anyone has the ball, Tecmo-style: control ball carrier if on your team; else nearest defender.
        if (ballCarrierId is not null)
        {
            var carrierTeam = _teamMapper.Get(ballCarrierId.Value);
            if (playerTeamIndex is not null && carrierTeam.TeamIndex == playerTeamIndex)
            {
                _control.Role = ControlRole.BallCarrier;
                return ballCarrierId;
            }

            // Defense: nearest player-team defender to the carrier.
            var nearest = FindNearestDefenderTo(ballCarrierId.Value, entities, playerTeamIndex);
            if (nearest is not null)
            {
                _control.Role = ControlRole.DefenderNearestBall;
                return nearest;
            }
        }

        // No ball: pre-snap default to QB if available.
        if (_loop is null || _loop.IsOnField("pre_snap"))
        {
            var qb = FindQuarterback(entities, playerTeamIndex);
            if (qb is not null)
            {
                _control.Role = ControlRole.Quarterback;
                return qb;
            }
        }

        // Fallback: keep current if still valid.
        if (_control.ControlledEntityId is not null)
        {
            foreach (var id in entities)
            {
                if (id == _control.ControlledEntityId.Value)
                    return id;
            }
        }

        // Otherwise, choose the first entity on the player team (or just first entity).
        foreach (var id in entities)
        {
            var team = _teamMapper.Get(id);
            if (playerTeamIndex is not null && team.TeamIndex == playerTeamIndex)
            {
                _control.Role = ControlRole.Unknown;
                return id;
            }
        }

        _control.Role = ControlRole.Unknown;
        return entities[0];
    }

    private int? ChooseNextOnSwitch(List<int> entities, int? playerTeamIndex, int? ballCarrierId)
    {
        // Minimal manual switching rules:
        // - Offense / pre-snap (no ball): cycle through player-team offensive entities.
        // - Defense (ball owned by other team): cycle through defenders by "closest-to-ball" ordering.
        // - Otherwise: cycle through all player-team entities by entityId.

        if (ballCarrierId is not null)
        {
            var carrierTeam = _teamMapper.Get(ballCarrierId.Value);
            if (playerTeamIndex is not null && carrierTeam.TeamIndex != playerTeamIndex)
            {
                return ChooseNextClosestDefender(ballCarrierId.Value, entities, playerTeamIndex);
            }
        }

        if ((_loop is null || _loop.IsOnField("pre_snap")) && ballCarrierId is null)
        {
            return ChooseNextByEntityId(entities, playerTeamIndex, offenseOnly: true);
        }

        return ChooseNextByEntityId(entities, playerTeamIndex, offenseOnly: false);
    }

    private int? ChooseNextByEntityId(List<int> entities, int? playerTeamIndex, bool offenseOnly)
    {
        var eligible = new List<int>();
        foreach (var id in entities)
        {
            var team = _teamMapper.Get(id);
            if (playerTeamIndex is not null && team.TeamIndex != playerTeamIndex)
                continue;
            if (!team.IsPlayerControlled)
                continue;
            if (offenseOnly && !team.IsOffense)
                continue;
            eligible.Add(id);
        }

        if (eligible.Count == 0)
            return null;

        var current = _control.ControlledEntityId;
        if (current is null)
            return eligible[0];

        for (var i = 0; i < eligible.Count; i++)
        {
            if (eligible[i] == current.Value)
                return eligible[(i + 1) % eligible.Count];
        }

        return eligible[0];
    }

    private int? ChooseNextClosestDefender(int ballCarrierId, List<int> entities, int? playerTeamIndex)
    {
        var ordered = GetDefendersByDistanceTo(ballCarrierId, entities, playerTeamIndex);
        if (ordered.Count == 0)
            return null;

        var current = _control.ControlledEntityId;
        if (current is null)
            return ordered[0];

        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i] == current.Value)
                return ordered[(i + 1) % ordered.Count];
        }

        return ordered[0];
    }

    private int? FindQuarterback(List<int> entities, int? playerTeamIndex)
    {
        foreach (var id in entities)
        {
            var team = _teamMapper.Get(id);
            if (!team.IsPlayerControlled)
                continue;
            if (playerTeamIndex is not null && team.TeamIndex != playerTeamIndex)
                continue;
            if (!team.IsOffense)
                continue;

            if (_attrMapper.Has(id))
            {
                var pos = (_attrMapper.Get(id).Position ?? string.Empty).Trim();
                if (string.Equals(pos, "QB", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pos, "Quarterback", StringComparison.OrdinalIgnoreCase))
                {
                    return id;
                }
            }
        }

        return null;
    }

    private int? FindNearestDefenderTo(int ballCarrierId, List<int> entities, int? playerTeamIndex)
    {
        var ordered = GetDefendersByDistanceTo(ballCarrierId, entities, playerTeamIndex);
        return ordered.Count == 0 ? null : ordered[0];
    }

    private List<int> GetDefendersByDistanceTo(int ballCarrierId, List<int> entities, int? playerTeamIndex)
    {
        var ballPos = _posMapper.Get(ballCarrierId).Position;

        // Build list of eligible defenders.
        var defenders = new List<(int id, float dist2)>();
        foreach (var id in entities)
        {
            if (id == ballCarrierId)
                continue;

            var team = _teamMapper.Get(id);
            if (!team.IsPlayerControlled)
                continue;
            if (playerTeamIndex is not null && team.TeamIndex != playerTeamIndex)
                continue;

            // Must be on defense.
            if (team.IsOffense)
                continue;

            var d = _posMapper.Get(id).Position - ballPos;
            defenders.Add((id, d.LengthSquared()));
        }

        defenders.Sort((a, b) =>
        {
            var c = a.dist2.CompareTo(b.dist2);
            return c != 0 ? c : a.id.CompareTo(b.id);
        });

        var ordered = new List<int>(defenders.Count);
        foreach (var d in defenders)
            ordered.Add(d.id);
        return ordered;
    }

    private void ApplySelection(List<int> entities, int? desired, int? playerTeamIndex, int? ballCarrierId)
    {
        // If selection came back null, clear all control flags.
        if (desired is null)
        {
            foreach (var id in entities)
                _controlMapper.Get(id).IsControlled = false;

            _control.SetControlledEntity(null);
            return;
        }

        // Enforce: only player-team, player-controlled entities can be selected.
        // (Selection helpers already attempt this, but this is a final guard.)
        if (!_teamMapper.Has(desired.Value))
            desired = null;
        else
        {
            var team = _teamMapper.Get(desired.Value);
            if (!team.IsPlayerControlled)
                desired = null;
            if (playerTeamIndex is not null && team.TeamIndex != playerTeamIndex)
                desired = null;
        }

        foreach (var id in entities)
            _controlMapper.Get(id).IsControlled = (desired is not null && id == desired.Value);

        _control.SetControlledEntity(desired);

        // Best-effort role refresh if not already set.
        if (desired is not null && _control.Role == ControlRole.Unknown)
        {
            if (ballCarrierId is not null && desired.Value == ballCarrierId.Value)
                _control.Role = ControlRole.BallCarrier;
        }
    }
}
