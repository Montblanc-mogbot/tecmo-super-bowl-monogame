using System.Collections.Generic;
using TecmoSBGame.Factories;

namespace TecmoSBGame.Spawning;

/// <summary>
/// Very small, deterministic roster abstraction.
///
/// The project does not yet have real player/ROM rosters, so this produces stable
/// placeholder names/jerseys/stats per role.
/// </summary>
public sealed class TeamRoster
{
    private readonly string _teamId;
    private readonly Dictionary<PlayerRoleKey, int> _counters = new();

    public TeamRoster(string teamId)
    {
        _teamId = string.IsNullOrWhiteSpace(teamId) ? "TEAM" : teamId.Trim();
    }

    public RosterPlayer Next(PlayerRoleKey role)
    {
        if (!_counters.TryGetValue(role, out var n))
            n = 0;
        n++;
        _counters[role] = n;

        // Deterministic jersey assignment, stable by role then ordinal.
        // (Not aiming for realism; aiming for legibility.)
        var jersey = role switch
        {
            PlayerRoleKey.QB => 7,
            PlayerRoleKey.K => 1,
            PlayerRoleKey.P => 2,
            _ => 10,
        } + (n - 1);

        var name = $"{_teamId}-{role}{n}";

        // Slight role-based stat shaping to make headless logs easier to eyeball.
        var stats = role switch
        {
            PlayerRoleKey.QB => new PlayerStats(Pa: 70, Ar: 60, Ms: 45, Rs: 45),
            PlayerRoleKey.RB => new PlayerStats(Rs: 65, Ms: 70, Bc: 65, Rec: 45),
            PlayerRoleKey.WR => new PlayerStats(Rs: 70, Ms: 75, Rec: 70, Bc: 55),
            PlayerRoleKey.TE => new PlayerStats(Rs: 55, Ms: 55, Rec: 55, Hp: 55),
            PlayerRoleKey.OL => new PlayerStats(Hp: 65, Rs: 35, Ms: 35, Rp: 60),
            PlayerRoleKey.DL => new PlayerStats(Hp: 70, Rs: 35, Ms: 35, Rp: 65),
            PlayerRoleKey.LB => new PlayerStats(Hp: 60, Rs: 50, Ms: 50, Rp: 60),
            PlayerRoleKey.DB => new PlayerStats(Hp: 50, Rs: 65, Ms: 70, Rec: 55),
            PlayerRoleKey.K => new PlayerStats(Kp: 75, Kab: 70, Ms: 40, Rs: 40),
            PlayerRoleKey.P => new PlayerStats(Kp: 70, Kab: 65, Ms: 40, Rs: 40),
            _ => new PlayerStats(),
        };

        return new RosterPlayer(role, name, jersey, stats);
    }
}

public readonly record struct RosterPlayer(
    PlayerRoleKey Role,
    string Name,
    int JerseyNumber,
    PlayerStats Stats);

/// <summary>
/// Roster role key: this is intentionally separate from the ECS <see cref="TecmoSBGame.Components.PlayerRole"/>
/// enum to keep the spawner independent from component types.
/// </summary>
public enum PlayerRoleKey
{
    Unknown = 0,
    QB,
    RB,
    WR,
    TE,
    OL,
    DL,
    LB,
    DB,
    K,
    P,
}
