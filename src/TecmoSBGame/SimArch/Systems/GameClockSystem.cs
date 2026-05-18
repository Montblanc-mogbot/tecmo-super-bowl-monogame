using System;
using TecmoSBGame.SimArch.Events;
using TecmoSBGame.SimArch.State;

namespace TecmoSBGame.SimArch.Systems;

// Ported from: src/TecmoSBGame/ArchiveMge/Systems/GameClockSystem.cs

/// <summary>
/// Deterministic 60Hz game clock rules (SimArch).
///
/// Simplified rules (ported from legacy MGE):
/// - clock runs only during live play (PlayState.Phase == InPlay)
/// - decrements once per 60 ticks
/// - quarter transitions at 0
/// </summary>
public sealed class GameClockSystem
{
    public const int QuarterLengthSeconds = 5 * 60;

    private int _ticksIntoSecond;
    private int _lastQuarterEndHandled;

    public void Update(MatchState match, PlayState play)
    {
        if (match.MatchOver)
            return;

        match.ClockRunning = play.Phase == PlayPhase.InPlay && match.Phase != MatchPhase.Halftime && match.Phase != MatchPhase.Final;
        if (!match.ClockRunning)
            return;

        _ticksIntoSecond++;
        if (_ticksIntoSecond < 60)
            return;

        _ticksIntoSecond -= 60;

        if (match.GameClockSeconds > 0)
            match.GameClockSeconds--;

        if (match.GameClockSeconds <= 0)
            HandleEndOfQuarterIfNeeded(match);
    }

    public void AdvanceFromHalftime(MatchState match)
    {
        if (match.Phase != MatchPhase.Halftime)
            return;

        match.PossessionTeam = match.DeferredKickReceivingTeam;
        match.OffenseDirection = match.PossessionTeam == 0 ? OffenseDirection.LeftToRight : OffenseDirection.RightToLeft;
        match.KickingTeamIndex = match.DeferredKickKickingTeam;
        match.ReceivingTeamIndex = match.DeferredKickReceivingTeam;
        match.Down = 1;
        match.YardsToGo = 10;
        match.GoalToGo = false;
        match.BallSpot = BallSpot.Own(25);
        match.Quarter = 3;
        match.GameClockSeconds = QuarterLengthSeconds;
        match.Phase = MatchPhase.ThirdQuarter;
        _ticksIntoSecond = 0;
    }

    private void HandleEndOfQuarterIfNeeded(MatchState match)
    {
        if (match.GameClockSeconds != 0)
            return;

        if (_lastQuarterEndHandled == match.Quarter)
            return;

        var endedQuarter = match.Quarter;
        _lastQuarterEndHandled = endedQuarter;

        var quarterEnded = new QuarterEndedEvent(endedQuarter);
        SimEventBus.Send(ref quarterEnded);

        if (endedQuarter == 2)
        {
            var halftime = new HalftimeEvent();
            SimEventBus.Send(ref halftime);
            match.Phase = MatchPhase.Halftime;
            match.DeferredKickReceivingTeam = match.ReceivingTeamIndex;
            match.DeferredKickKickingTeam = match.KickingTeamIndex;
            match.PossessionTeam = match.DeferredKickReceivingTeam;
            match.OffenseDirection = match.PossessionTeam == 0 ? OffenseDirection.LeftToRight : OffenseDirection.RightToLeft;
            match.Down = 1;
            match.YardsToGo = 10;
            match.GoalToGo = false;
            match.BallSpot = BallSpot.Own(25);
            _ticksIntoSecond = 0;
            return;
        }

        if (endedQuarter >= 4)
        {
            match.MatchOver = true;
            match.Phase = MatchPhase.Final;
            var ended = new GameEndedEvent(endedQuarter);
            SimEventBus.Send(ref ended);
            _ticksIntoSecond = 0;
            return;
        }

        match.Quarter = endedQuarter + 1;
        match.GameClockSeconds = QuarterLengthSeconds;
        match.Phase = match.Quarter == 2 ? MatchPhase.SecondQuarter : MatchPhase.FourthQuarter;
        match.OffenseDirection = match.OffenseDirection == OffenseDirection.LeftToRight
            ? OffenseDirection.RightToLeft
            : OffenseDirection.LeftToRight;
        _ticksIntoSecond = 0;
    }
}
