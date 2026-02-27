using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.Field;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Authoritative aggregator for play-ending signals.
///
/// Responsibilities:
/// - Observe <see cref="WhistleEvent"/> (without consuming it) and finalize <see cref="PlayState"/>.
/// - Derive simple outcome flags (TD/safety/turnover) from end spot + possession.
/// - Advance <see cref="MatchState"/> minimally (score, possession, down/distance, spot).
///
/// Notes:
/// - Keep rules conservative and deterministic; Tecmo-accurate edge cases can be layered later.
/// - Uses Read() (not Drain) so <see cref="LoopMachineSystem"/> can also observe whistles.
/// </summary>
public sealed class PlayEndSystem : EntityUpdateSystem
{
    private readonly GameEvents? _events;
    private readonly MatchState _match;
    private readonly PlayState _play;
    private readonly bool _log;

    private ComponentMapper<BallComponent> _ballTag = null!;
    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<TeamComponent> _team = null!;

    private int _lastProcessedPlayId = -1;

    public PlayEndSystem(GameEvents? events, MatchState matchState, PlayState playState, bool log = true)
        : base(Aspect.All(typeof(PositionComponent)))
    {
        _events = events;
        _match = matchState;
        _play = playState;
        _log = log;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _ballTag = mapperService.GetMapper<BallComponent>();
        _pos = mapperService.GetMapper<PositionComponent>();
        _team = mapperService.GetMapper<TeamComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (_events is null)
            return;

        // Process at most once per play id.
        if (_play.PlayId == _lastProcessedPlayId)
            return;

        var whistles = _events.Read<WhistleEvent>();
        if (whistles.Count <= 0)
            return;

        // Choose the first non-empty whistle reason as the tick's play end cause.
        WhistleReason reason = _play.WhistleReason != WhistleReason.None
            ? _play.WhistleReason
            : WhistleReason.None;

        if (reason == WhistleReason.None)
        {
            for (var i = 0; i < whistles.Count; i++)
            {
                var parsed = ParseWhistleReason(whistles[i].Reason);
                if (parsed != WhistleReason.Other)
                {
                    reason = parsed;
                    break;
                }

                if (reason == WhistleReason.None)
                    reason = parsed; // may still be Other
            }
        }

        if (reason == WhistleReason.None)
            reason = WhistleReason.Other;

        // Finalize play model.
        if (_play.WhistleReason == WhistleReason.None)
            _play.WhistleReason = reason;

        _play.Phase = PlayPhase.PostPlay;
        _play.BallState = BallState.Dead;

        // Ensure we have an end spot.
        if (TryGetBallEndAbsoluteYard(out var ballEndAbs))
        {
            // Many whistle publishers already set EndAbsoluteYard, but incompletions might not.
            // Only override when the end spot looks uninitialized.
            if (_play.EndAbsoluteYard == 0 && _play.StartAbsoluteYard != 0)
                _play.EndAbsoluteYard = ballEndAbs;

            if (_play.EndAbsoluteYard == _play.StartAbsoluteYard && reason is WhistleReason.Incomplete or WhistleReason.OutOfBounds or WhistleReason.Touchback or WhistleReason.Safety)
                _play.EndAbsoluteYard = ballEndAbs;
        }

        // Yards gained is from offense's own-goal perspective.
        var startDist = PlayState.DistFromOwnGoal(_play.StartAbsoluteYard, _match.OffenseDirection);
        var endDist = PlayState.DistFromOwnGoal(_play.EndAbsoluteYard, _match.OffenseDirection);
        var yards = endDist - startDist;

        // Keep incomplete as 0 yards for now.
        if (reason == WhistleReason.Incomplete)
            yards = 0;

        var result = _play.Result;
        result = result with { YardsGained = yards };

        // Determine possession at end.
        var offenseTeam = _match.PossessionTeam;
        var endOwnerTeam = offenseTeam;
        if (_play.BallOwnerEntityId is int ownerId && _team.Has(ownerId))
            endOwnerTeam = _team.Get(ownerId).TeamIndex;

        var turnover = result.Turnover || (endOwnerTeam != offenseTeam);
        result = result with { Turnover = turnover };

        // Touchdown/safety checks (simplified): based on end spot + who possesses at end.
        // TODO(Tecmo parity): handle loose-ball end zone rules and possession changes in end zone.
        var oppGoalAbs = _match.OffenseDirection == OffenseDirection.LeftToRight ? 100 : 0;
        var ownGoalAbs = 100 - oppGoalAbs;
        var offensePossessesAtEnd = endOwnerTeam == offenseTeam;

        var touchdown = result.Touchdown;
        var safety = result.Safety;

        if (!touchdown && offensePossessesAtEnd && _play.EndAbsoluteYard == oppGoalAbs)
            touchdown = true;

        if (!safety && offensePossessesAtEnd && _play.EndAbsoluteYard == ownGoalAbs)
            safety = true;

        result = result with { Touchdown = touchdown, Safety = safety };

        // Allow derived scoring to override the generic whistle reason.
        if (touchdown)
            _play.WhistleReason = WhistleReason.Touchdown;
        else if (safety)
            _play.WhistleReason = WhistleReason.Safety;

        _play.Result = result;

        // Advance match state once.
        _match.PlayNumber++;

        // Scoring.
        if (touchdown)
            _match.AddScore(offenseTeam, 6);
        if (safety)
            _match.AddScore(1 - offenseTeam, 2);

        // Possession changes.
        var newPossTeam = offenseTeam;
        if (touchdown)
            newPossTeam = 1 - offenseTeam; // kickoff to other team (placeholder)
        else if (safety)
            newPossTeam = 1 - offenseTeam; // free kick to scoring team (placeholder)
        else if (turnover)
            newPossTeam = endOwnerTeam;

        if (newPossTeam != offenseTeam)
        {
            _match.PossessionTeam = newPossTeam;
            _match.DriveId++;
            _match.Down = 1;
            _match.YardsToGo = 10;

            // Simple offense direction convention.
            _match.OffenseDirection = newPossTeam == 0 ? OffenseDirection.LeftToRight : OffenseDirection.RightToLeft;
        }
        else if (!touchdown && !safety)
        {
            var firstDown = yards >= _match.YardsToGo;
            _match.AdvanceDownDistance(yardsGained: yards, firstDown: firstDown);
        }

        // Spot ball for next snap.
        if (touchdown)
        {
            // Placeholder: after TD, spot at own 25 for the team that now has possession.
            _match.BallSpot = BallSpot.Own(25);
        }
        else if (_play.WhistleReason == WhistleReason.Touchback)
        {
            _match.BallSpot = BallSpot.Own(25);
        }
        else
        {
            // Clamp away from 0/100 for non-scoring spots.
            var spotAbs = Math.Clamp(_play.EndAbsoluteYard, 1, 99);
            _match.SpotBallAbsoluteYard(spotAbs);
        }

        // Publish a derived end event for any future consumers.
        _events.Publish(new PlayEndedEvent(
            PlayId: _play.PlayId,
            Reason: _play.WhistleReason,
            EndAbsoluteYard: _play.EndAbsoluteYard,
            YardsGained: yards,
            Turnover: turnover,
            Touchdown: touchdown,
            Safety: safety));

        if (_log)
        {
            Console.WriteLine($"[play-end] reason={_play.WhistleReason} endAbs={_play.EndAbsoluteYard} yards={yards} result(TD={touchdown} S={safety} TO={turnover}) | score {_match.Team0Score}-{_match.Team1Score} poss=T{_match.PossessionTeam} { _match.FormatDownDistance() } @ {_match.BallSpot}");
        }

        _lastProcessedPlayId = _play.PlayId;
    }

    private bool TryGetBallEndAbsoluteYard(out int endAbs)
    {
        foreach (var id in ActiveEntities)
        {
            if (!_ballTag.Has(id))
                continue;

            endAbs = FieldBounds.XToAbsoluteYard(_pos.Get(id).Position.X);
            return true;
        }

        endAbs = 0;
        return false;
    }

    private static WhistleReason ParseWhistleReason(string? reason)
    {
        reason = (reason ?? string.Empty).Trim().ToLowerInvariant();

        // Allow namespaced reasons like "bounds:oob".
        var idx = reason.IndexOf(':');
        if (idx >= 0 && idx + 1 < reason.Length)
            reason = reason[(idx + 1)..];

        return reason switch
        {
            "tackle" => WhistleReason.Tackle,
            "oob" or "outofbounds" or "out_of_bounds" => WhistleReason.OutOfBounds,
            "td" or "touchdown" => WhistleReason.Touchdown,
            "safety" => WhistleReason.Safety,
            "touchback" or "tb" => WhistleReason.Touchback,
            "incomplete" => WhistleReason.Incomplete,
            "turnover" => WhistleReason.Turnover,
            "" => WhistleReason.Other,
            _ => WhistleReason.Other,
        };
    }
}
