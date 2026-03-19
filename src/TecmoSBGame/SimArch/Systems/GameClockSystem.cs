using System;
using TecmoSBGame.SimArch.State;

namespace TecmoSBGame.SimArch.Systems;

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

        if (play.Phase != PlayPhase.InPlay)
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

    private void HandleEndOfQuarterIfNeeded(MatchState match)
    {
        if (match.GameClockSeconds != 0)
            return;

        if (_lastQuarterEndHandled == match.Quarter)
            return;

        var endedQuarter = match.Quarter;
        _lastQuarterEndHandled = endedQuarter;

        if (endedQuarter >= 4)
        {
            match.MatchOver = true;
            return;
        }

        match.Quarter = endedQuarter + 1;
        match.GameClockSeconds = QuarterLengthSeconds;
        _ticksIntoSecond = 0;
    }
}
