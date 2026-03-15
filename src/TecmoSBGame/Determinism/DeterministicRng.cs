using System;

namespace TecmoSBGame.Determinism;

/// <summary>
/// Tiny deterministic hash-based RNG helpers.
///
/// Goal: identical inputs => identical outputs across platforms/runtimes.
/// This is not cryptographic randomness; it's for repeatable AI decisions.
/// </summary>
public static class DeterministicRng
{
    public static uint Mix(uint a, uint b)
    {
        // 32-bit mix (xorshift-ish) - stable and cheap.
        uint x = a ^ (b + 0x9E3779B9u + (a << 6) + (a >> 2));
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        return x;
    }

    public static uint Mix(uint a, uint b, uint c) => Mix(Mix(a, b), c);
    public static uint Mix(uint a, uint b, uint c, uint d) => Mix(Mix(a, b, c), d);

    public static float Float01(uint seed, uint salt)
    {
        var x = Mix(seed, salt);
        // Convert top 24 bits to [0,1).
        return ((x >> 8) & 0xFFFFFFu) / 16777216f;
    }

    public static int Range(uint seed, uint salt, int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            return minInclusive;

        var x = Mix(seed, salt);
        var span = (uint)(maxExclusive - minInclusive);
        return (int)(x % span) + minInclusive;
    }

    public static uint SeedFromPlay(uint baseSeed, int playId, int startAbsoluteYard)
    {
        return Mix(baseSeed, (uint)playId, (uint)startAbsoluteYard);
    }

    public static uint SeedFromMatchup(int homeTeam, int awayTeam)
    {
        // Some stable matchup seed; callers can Mix in more.
        return Mix((uint)homeTeam, (uint)awayTeam, 0xC0FEBABEu);
    }
}
