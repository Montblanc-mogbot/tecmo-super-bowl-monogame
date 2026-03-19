using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Dedicated rules/refereeing system for down &amp; distance progression and ball spotting.
///
/// Consumes <see cref="PlayEndedEvent"/> and applies conservative deterministic updates to <see cref="MatchState"/>.
///
/// TODO(Tecmo parity):
/// - Goal-to-go handling and varying YTG inside the 10.
/// - Kickoff/punt/FG special cases.
/// - Safety possession (free kick rules).
/// - Turnover spot nuances (returns into end zone, touchbacks, etc.).
/// - First-down by penalty.
/// </summary>
public sealed class DownDistanceSystem : UpdateSystem
{
    private readonly GameEvents? _events;
    private readonly MatchState _match;
    private readonly bool _log;

    public DownDistanceSystem(GameEvents? events, MatchState matchState, bool log = true)
    {
        _events = events;
        _match = matchState;
        _log = log;
    }

    public override void Update(GameTime gameTime)
    {
        if (_events is null)
            return;

        // Use Read() so other systems (eg, KickoffAfterScoreSystem) can also observe PlayEndedEvent.
        // BeginTick() clears events at the start of each simulation tick.
        var ended = _events.Read<PlayEndedEvent>();
        for (var i = 0; i < ended.Count; i++)
            Apply(ended[i]);
    }

    private void Apply(PlayEndedEvent e)
    {
        // Advance match state once per play end.
        _match.PlayNumber++;

        var offenseTeam = _match.PossessionTeam;

        // Scoring (simple for now).
        if (e.Touchdown)
            _match.AddScore(offenseTeam, 6);

        if (e.Safety)
            _match.AddScore(1 - offenseTeam, 2);

        // Possession changes.
        // NOTE: PlayEndedEvent currently only has a Turnover flag (not the new team),
        // so we assume a turnover flips possession.
        // TODO(Tecmo parity): include the end possession team in PlayEndedEvent.
        //
        // IMPORTANT: Do NOT mutate possession on scoring plays here; KickoffAfterScoreSystem
        // performs the deterministic scoring->kickoff transition and needs to observe
        // the original offense team.
        var newPossTeam = offenseTeam;

        if (!e.Touchdown && !e.Safety && e.Turnover)
            newPossTeam = 1 - offenseTeam;

        if (newPossTeam != offenseTeam)
        {
            _match.PossessionTeam = newPossTeam;
            _match.DriveId++;

            // Simple offense direction convention.
            _match.OffenseDirection = newPossTeam == 0 ? OffenseDirection.LeftToRight : OffenseDirection.RightToLeft;

            _match.Down = 1;
            _match.YardsToGo = 10;
        }
        else if (!e.Touchdown && !e.Safety)
        {
            var firstDown = e.YardsGained >= _match.YardsToGo;
            _match.AdvanceDownDistance(yardsGained: e.YardsGained, firstDown: firstDown);
        }

        // Spot ball for next snap.
        if (e.Touchdown || e.Safety)
        {
            // KickoffAfterScoreSystem will set the kickoff spot deterministically.
        }
        else if (e.Reason == WhistleReason.Touchback)
        {
            _match.SpotBall(BallSpot.Own(25));
        }
        else
        {
            // Clamp away from the goal lines for non-scoring spots.
            var spotAbs = Math.Clamp(e.EndAbsoluteYard, 1, 99);
            _match.SpotBallAbsoluteYard(spotAbs);
        }

        if (_log)
        {
            Console.WriteLine($"[rules] playId={e.PlayId} reason={e.Reason} endAbs={e.EndAbsoluteYard} yards={e.YardsGained} TD={e.Touchdown} S={e.Safety} TO={e.Turnover} | poss=T{_match.PossessionTeam} {_match.FormatDownDistance()} @ {_match.BallSpot} | score {_match.Team0Score}-{_match.Team1Score}");
        }
    }
}
