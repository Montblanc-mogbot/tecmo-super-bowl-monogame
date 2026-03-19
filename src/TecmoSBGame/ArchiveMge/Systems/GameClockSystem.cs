using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Deterministic 60Hz game clock rules.
///
/// Current simplified rules:
/// - The clock runs only during live play.
///   - When <see cref="LoopState"/> is available, "live play" is defined as OnField state id == "live_play".
///   - In headless contexts without loop machines, we fall back to <see cref="PlayState.Phase"/> == InPlay.
/// - The clock stops outside live play (pre-snap, dead ball, whistles, scoring).
///
/// TODO(Tecmo parity):
/// - Stop clock on specific whistle reasons (incomplete, out-of-bounds, etc.) and apply "ready for play" runoff.
/// - Handle end-of-quarter semantics (quarter ends at end of play when time expires).
/// - Timeouts, spikes, kneels, hurry-up, etc.
/// </summary>
public sealed class GameClockSystem : UpdateSystem
{
    // NES Tecmo quarters are 5:00.
    public const int QuarterLengthSeconds = 5 * 60;

    private readonly GameEvents? _events;
    private readonly MatchState _match;
    private readonly PlayState? _play;
    private readonly LoopState? _loop;
    private readonly bool _log;

    // Deterministic fractional-second tracking: count 60Hz ticks.
    private int _ticksIntoSecond;

    // Prevent double-handling when a caller leaves the clock at 0.
    private int _lastQuarterEndHandled;

    public GameClockSystem(GameEvents? events, MatchState matchState, PlayState? playState = null, LoopState? loopState = null, bool log = true)
    {
        _events = events;
        _match = matchState ?? throw new ArgumentNullException(nameof(matchState));
        _play = playState;
        _loop = loopState;
        _log = log;

        _ticksIntoSecond = 0;
        _lastQuarterEndHandled = 0;
    }

    public override void Update(GameTime gameTime)
    {
        if (_match.MatchOver)
            return;

        var running = ShouldRunClock();
        if (!running)
            return;

        // Advance 60Hz ticks and decrement whole seconds deterministically.
        _ticksIntoSecond++;
        if (_ticksIntoSecond < 60)
            return;

        _ticksIntoSecond -= 60;

        if (_match.GameClockSeconds > 0)
            _match.GameClockSeconds--;

        if (_match.GameClockSeconds <= 0)
            HandleEndOfQuarterIfNeeded();
    }

    private bool ShouldRunClock()
    {
        // Prefer authoritative YAML loop state.
        if (_loop is not null)
            return _loop.IsOnField("live_play");

        // Headless fallback.
        if (_play is not null)
            return _play.Phase == PlayPhase.InPlay;

        return false;
    }

    private void HandleEndOfQuarterIfNeeded()
    {
        // If the clock is at 0, handle at most once for the current quarter.
        if (_match.GameClockSeconds != 0)
            return;

        if (_lastQuarterEndHandled == _match.Quarter)
            return;

        var endedQuarter = _match.Quarter;
        _lastQuarterEndHandled = endedQuarter;

        _events?.Publish(new QuarterEndedEvent(Quarter: endedQuarter));

        if (_log)
            Console.WriteLine($"[clock] end Q{endedQuarter}");

        // Halftime (end of Q2).
        if (endedQuarter == 2)
        {
            _events?.Publish(new HalftimeEvent());
            if (_log)
                Console.WriteLine("[clock] halftime");
        }

        // End of regulation (end of Q4).
        if (endedQuarter >= 4)
        {
            _match.MatchOver = true;
            _events?.Publish(new GameEndedEvent(FinalQuarter: endedQuarter));

            // Best-effort: raise a loop event via the existing whistle bridge.
            // (The YAML on-field loop listens for "end_of_game" in dead_ball.)
            _events?.Publish(new WhistleEvent("end_of_game"));

            if (_log)
                Console.WriteLine($"[clock] game over (end Q{endedQuarter})");

            return;
        }

        // Start next quarter.
        _match.Quarter = endedQuarter + 1;
        _match.GameClockSeconds = QuarterLengthSeconds;
        _ticksIntoSecond = 0;

        // Best-effort: notify loop machines (if/when they handle it).
        _events?.Publish(new WhistleEvent("end_of_quarter"));

        if (_log)
            Console.WriteLine($"[clock] start Q{_match.Quarter} {MatchState.FormatClock(_match.GameClockSeconds)}");
    }
}
