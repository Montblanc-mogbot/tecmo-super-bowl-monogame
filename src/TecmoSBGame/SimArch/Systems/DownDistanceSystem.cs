using System;
using TecmoSBGame.SimArch.State;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Down & distance progression + ball spotting (SimArch scaffold).
///
/// Consumes play end information from PlayState and applies updates to MatchState.
///
/// NOTE: This is intentionally conservative and does not cover full Tecmo parity.
/// </summary>
public sealed class DownDistanceSystem
{
    public void ApplyPlayEnd(MatchState match, PlayState play)
    {
        if (!play.IsOver)
            return;

        match.PlayNumber++;

        var offenseTeam = match.PossessionTeam;

        if (play.Result.Touchdown)
            match.AddScore(offenseTeam, 6);

        if (play.Result.Safety)
            match.AddScore(1 - offenseTeam, 2);

        var newPossTeam = offenseTeam;
        if (!play.Result.Touchdown && !play.Result.Safety && play.Result.Turnover)
            newPossTeam = 1 - offenseTeam;

        if (newPossTeam != offenseTeam)
        {
            match.PossessionTeam = newPossTeam;
            match.DriveId++;

            match.OffenseDirection = newPossTeam == 0 ? OffenseDirection.LeftToRight : OffenseDirection.RightToLeft;
            match.Down = 1;
            match.YardsToGo = 10;
        }
        else if (!play.Result.Touchdown && !play.Result.Safety)
        {
            var firstDown = play.Result.YardsGained >= match.YardsToGo;
            match.AdvanceDownDistance(play.Result.YardsGained, firstDown);
        }

        if (play.Result.Touchdown || play.Result.Safety)
        {
            // scoring transition handled elsewhere
        }
        else if (play.WhistleReason == WhistleReason.Touchback)
        {
            match.SpotBall(BallSpot.Own(25));
        }
        else
        {
            var spotAbs = Math.Clamp(play.EndAbsoluteYard, 1, 99);
            match.SpotBallAbsoluteYard(spotAbs);
        }
    }
}
