using Microsoft.Xna.Framework;

namespace TecmoSBGame.Timing;

/// <summary>
/// Fixed-timestep simulation driver.
/// 
/// - Accumulates real elapsed time (variable per frame)
/// - Executes 0..N fixed 60Hz simulation ticks per frame
/// - Caps catch-up work to avoid a spiral of death
/// 
/// This is intentionally small and deterministic: simulation ticks always advance by exactly
/// <see cref="FixedElapsed"/> and <see cref="Total"/> only advances when a tick is executed.
/// </summary>
public sealed class FixedTimestepRunner
{
    private TimeSpan _accumulator;

    public FixedTimestepRunner(int hz, int maxTicksPerFrame = 5, TimeSpan? maxAccumulated = null)
    {
        if (hz <= 0) throw new ArgumentOutOfRangeException(nameof(hz));
        if (maxTicksPerFrame <= 0) throw new ArgumentOutOfRangeException(nameof(maxTicksPerFrame));

        Hz = hz;
        FixedElapsed = TimeSpan.FromSeconds(1.0 / hz);
        MaxTicksPerFrame = maxTicksPerFrame;

        // Clamp the accumulator so a breakpoint / hitch doesn't cause minutes of catch-up.
        // Default: 250ms (up to 15 ticks at 60Hz), but we still obey MaxTicksPerFrame.
        MaxAccumulated = maxAccumulated ?? TimeSpan.FromMilliseconds(250);

        _accumulator = TimeSpan.Zero;
        Total = TimeSpan.Zero;
    }

    public int Hz { get; }
    public TimeSpan FixedElapsed { get; }
    public int MaxTicksPerFrame { get; }
    public TimeSpan MaxAccumulated { get; }

    /// <summary>
    /// Total simulated time advanced by fixed ticks.
    /// </summary>
    public TimeSpan Total { get; private set; }

    public void Reset(TimeSpan? total = null)
    {
        _accumulator = TimeSpan.Zero;
        Total = total ?? TimeSpan.Zero;
    }

    /// <summary>
    /// Accumulate real elapsed time and execute up to <see cref="MaxTicksPerFrame"/> fixed ticks.
    /// Returns the number of ticks executed.
    /// </summary>
    public int Advance(TimeSpan realElapsed, Action<GameTime> tick)
    {
        if (tick is null) throw new ArgumentNullException(nameof(tick));
        if (realElapsed < TimeSpan.Zero) realElapsed = TimeSpan.Zero;

        _accumulator += realElapsed;
        if (_accumulator > MaxAccumulated)
            _accumulator = MaxAccumulated;

        var ticks = 0;
        while (_accumulator >= FixedElapsed && ticks < MaxTicksPerFrame)
        {
            _accumulator -= FixedElapsed;
            Total += FixedElapsed;
            ticks++;

            tick(new GameTime(Total, FixedElapsed));
        }

        return ticks;
    }

    /// <summary>
    /// Convenience for deterministic headless runs: execute exactly one fixed tick.
    /// </summary>
    public void TickOnce(Action<GameTime> tick)
    {
        if (tick is null) throw new ArgumentNullException(nameof(tick));

        Total += FixedElapsed;
        tick(new GameTime(Total, FixedElapsed));
    }
}
