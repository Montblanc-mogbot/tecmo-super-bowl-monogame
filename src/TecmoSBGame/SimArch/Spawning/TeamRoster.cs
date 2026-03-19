using System.Collections.Generic;

namespace TecmoSBGame.SimArch.Spawning;

/// <summary>
/// Simple roster container used by spawners.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Spawning/TeamRoster.cs
/// </summary>
public sealed class TeamRoster
{
    public int TeamIndex { get; init; }
    public bool IsOffense { get; init; }

    /// <summary>
    /// Entity ids for spawned players.
    /// </summary>
    public List<int> Players { get; } = new();
}
