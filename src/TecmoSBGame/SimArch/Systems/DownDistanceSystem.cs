using System;
using TecmoSBGame.SimArch.Events;
using TecmoSBGame.SimArch.State;

namespace TecmoSBGame.SimArch.Systems;

// Ported from: src/TecmoSBGame/ArchiveMge/Systems/DownDistanceSystem.cs

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

        var offenseTeam = match.PossessionTeam;
        var defenseTeam = 1 - offenseTeam;
        var currentYardsToGo = match.YardsToGo;
        var endSpotAbs = Math.Clamp(play.EndAbsoluteYard, 0, 100);

        if (match.KickoffPlayActive)
        {
            ApplyKickoffEnd(match, play, offenseTeam, defenseTeam, endSpotAbs);
            return;
        }

        if (match.PuntPlayActive)
        {
            ApplyPuntEnd(match, play, offenseTeam, defenseTeam, endSpotAbs);
            return;
        }

        if (match.FieldGoalPlayActive)
        {
            ApplyFieldGoalEnd(match, play, offenseTeam, defenseTeam, endSpotAbs);
            return;
        }

        if (play.Result.Touchdown)
        {
            match.AddScore(offenseTeam, 6);
            match.SpotBall(BallSpot.Own(MatchState.TouchbackSpotYard));
            match.ResetSeries();
            return;
        }

        if (play.Result.Safety)
        {
            match.AddScore(defenseTeam, 2);
            match.SpotBall(BallSpot.Own(MatchState.TouchbackSpotYard));
            match.ResetSeries();
            return;
        }

        if (play.WhistleReason == WhistleReason.Touchback)
            endSpotAbs = offenseTeam == 0 ? MatchState.TouchbackSpotYard : 100 - MatchState.TouchbackSpotYard;

        if (play.Result.Turnover)
        {
            FlipPossession(match, defenseTeam, endSpotAbs);
            return;
        }

        var firstDown = play.Result.YardsGained >= currentYardsToGo;
        match.SpotBallAbsoluteYard(endSpotAbs);

        if (!firstDown && match.Down >= 4)
        {
            FlipPossession(match, defenseTeam, endSpotAbs);
            return;
        }

        match.AdvanceDownDistance(play.Result.YardsGained, firstDown);
    }

    private static void FlipPossession(MatchState match, int newPossessionTeam, int endSpotAbs)
    {
        match.PossessionTeam = newPossessionTeam;
        match.DriveId++;
        match.OffenseDirection = newPossessionTeam == 0 ? OffenseDirection.LeftToRight : OffenseDirection.RightToLeft;
        match.SpotBallAbsoluteYard(endSpotAbs);
        match.ResetSeries();
    }

    private static void ApplyKickoffEnd(MatchState match, PlayState play, int offenseTeam, int defenseTeam, int endSpotAbs)
    {
        var nextPossession = play.Result.Turnover ? defenseTeam : offenseTeam;
        var receivingSpot = play.WhistleReason == WhistleReason.Touchback
            ? (nextPossession == 0 ? MatchState.TouchbackSpotYard : 100 - MatchState.TouchbackSpotYard)
            : endSpotAbs;

        match.PossessionTeam = nextPossession;
        match.DriveId++;
        match.OffenseDirection = nextPossession == 0 ? OffenseDirection.LeftToRight : OffenseDirection.RightToLeft;
        match.SpotBallAbsoluteYard(receivingSpot);
        match.ResetSeries();
        match.KickoffPending = false;
        match.KickoffPlayActive = false;
        match.PendingKickoffReason = null;
        match.KickoffLandingAbsoluteYardOverride = null;
    }

    private static void ApplyPuntEnd(MatchState match, PlayState play, int offenseTeam, int defenseTeam, int endSpotAbs)
    {
        var nextPossession = defenseTeam;
        var receivingSpot = play.WhistleReason == WhistleReason.Touchback
            ? (nextPossession == 0 ? MatchState.TouchbackSpotYard : 100 - MatchState.TouchbackSpotYard)
            : endSpotAbs;

        match.PossessionTeam = nextPossession;
        match.DriveId++;
        match.OffenseDirection = nextPossession == 0 ? OffenseDirection.LeftToRight : OffenseDirection.RightToLeft;
        match.SpotBallAbsoluteYard(receivingSpot);
        match.ResetSeries();
        match.PuntPending = false;
        match.PuntPlayActive = false;
        match.PuntLandingAbsoluteYardOverride = null;
        match.ForcePuntMuff = false;
    }

    private static void ApplyFieldGoalEnd(MatchState match, PlayState play, int offenseTeam, int defenseTeam, int endSpotAbs)
    {
        var wasExtraPoint = match.ExtraPointPending;
        var scored = play.Result.Touchdown;

        match.FieldGoalPending = false;
        match.FieldGoalPlayActive = false;
        match.FieldGoalTargetAbsoluteYardOverride = null;
        match.ForceFieldGoalBlock = false;
        match.ForceFieldGoalMiss = false;
        match.ExtraPointPending = false;

        if (scored)
        {
            match.AddScore(offenseTeam, wasExtraPoint ? 1 : 3);
            match.SpotBall(BallSpot.Own(MatchState.TouchbackSpotYard));
            match.ResetSeries();
            match.ResetForKickoff(kickingTeam: offenseTeam, receivingTeam: defenseTeam, reason: KickoffSetupReason.AfterTouchdown);
            return;
        }

        var nextPossession = defenseTeam;
        match.PossessionTeam = nextPossession;
        match.DriveId++;
        match.OffenseDirection = nextPossession == 0 ? OffenseDirection.LeftToRight : OffenseDirection.RightToLeft;
        match.SpotBallAbsoluteYard(wasExtraPoint ? endSpotAbs : play.StartAbsoluteYard);
        match.ResetSeries();
    }
}
