using System;
using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Spawning;

/// <summary>
/// Spawns a baseline scrimmage roster into the Arch world.
///
/// NOTE: Initial implementation is a deterministic demo roster. We'll later drive this from
/// formation YAML once the Arch sim has parity.
/// </summary>
public static class FormationSpawner
{
    public static (List<int> offenseEntityIds, List<int> defenseEntityIds, int ballEntityId) SpawnDemoScrimmage(World world)
    {
        // Offense: basic formation aligned around the middle of the field.
        var offense = new List<int>(11);
        var defense = new List<int>(11);

        var origin = new Vector2(128, 112);
        var losY = origin.Y;

        int SpawnPlayer(RoleId role, Vector2 offset, bool isOffense, int teamIndex)
        {
            var e = world.Create();

            e.Add(new Position { Value = origin + offset });
            e.Add(new Velocity { Value = Vector2.Zero });
            e.Add(new Team { TeamIndex = teamIndex, IsOffense = isOffense, IsPlayerControlled = isOffense });
            e.Add(new Role { Id = role });
            e.Add(new Behavior { State = BehaviorState.Idle, TargetEntityId = -1, TargetPosition = Vector2.Zero, StateTimer = 0f });

            return e.Id;
        }

        // Offense team=1, defense team=0 (matches current scrimmage log output).
        const int offTeam = 1;
        const int defTeam = 0;

        // QB/HB/FB
        offense.Add(SpawnPlayer(RoleId.QB, new Vector2(0, -12), isOffense: true, teamIndex: offTeam));
        offense.Add(SpawnPlayer(RoleId.HB, new Vector2(16, -4), isOffense: true, teamIndex: offTeam));
        offense.Add(SpawnPlayer(RoleId.FB, new Vector2(-16, -4), isOffense: true, teamIndex: offTeam));

        // WR/TE
        offense.Add(SpawnPlayer(RoleId.WR1, new Vector2(-64, -20), isOffense: true, teamIndex: offTeam));
        offense.Add(SpawnPlayer(RoleId.WR2, new Vector2(64, -20), isOffense: true, teamIndex: offTeam));
        offense.Add(SpawnPlayer(RoleId.TE, new Vector2(24, -8), isOffense: true, teamIndex: offTeam));

        // OL
        offense.Add(SpawnPlayer(RoleId.OC, new Vector2(0, 0), isOffense: true, teamIndex: offTeam));
        offense.Add(SpawnPlayer(RoleId.LG, new Vector2(-16, 0), isOffense: true, teamIndex: offTeam));
        offense.Add(SpawnPlayer(RoleId.RG, new Vector2(16, 0), isOffense: true, teamIndex: offTeam));
        offense.Add(SpawnPlayer(RoleId.LT, new Vector2(-32, 0), isOffense: true, teamIndex: offTeam));
        offense.Add(SpawnPlayer(RoleId.RT, new Vector2(32, 0), isOffense: true, teamIndex: offTeam));

        // Defense (simple front 4 / LB 4 / DB 3)
        defense.Add(SpawnPlayer(RoleId.DL1, new Vector2(-24, 16), isOffense: false, teamIndex: defTeam));
        defense.Add(SpawnPlayer(RoleId.DL2, new Vector2(-8, 16), isOffense: false, teamIndex: defTeam));
        defense.Add(SpawnPlayer(RoleId.DL3, new Vector2(8, 16), isOffense: false, teamIndex: defTeam));
        defense.Add(SpawnPlayer(RoleId.DL4, new Vector2(24, 16), isOffense: false, teamIndex: defTeam));

        defense.Add(SpawnPlayer(RoleId.LB1, new Vector2(-40, 32), isOffense: false, teamIndex: defTeam));
        defense.Add(SpawnPlayer(RoleId.LB2, new Vector2(-16, 32), isOffense: false, teamIndex: defTeam));
        defense.Add(SpawnPlayer(RoleId.LB3, new Vector2(16, 32), isOffense: false, teamIndex: defTeam));
        defense.Add(SpawnPlayer(RoleId.LB4, new Vector2(40, 32), isOffense: false, teamIndex: defTeam));

        defense.Add(SpawnPlayer(RoleId.CB1, new Vector2(-56, 56), isOffense: false, teamIndex: defTeam));
        defense.Add(SpawnPlayer(RoleId.CB2, new Vector2(56, 56), isOffense: false, teamIndex: defTeam));
        defense.Add(SpawnPlayer(RoleId.S1, new Vector2(0, 72), isOffense: false, teamIndex: defTeam));

        // Ball entity: start held by QB
        var qbId = offense[0];
        var ball = world.Create();
        ball.Add(new Position { Value = origin + new Vector2(0, -12) });
        ball.Add(new Velocity { Value = Vector2.Zero });
        ball.Add(new Ball
        {
            State = TecmoSBGame.SimArch.Components.BallState.Held,
            OwnerEntityId = qbId,
            FlightKind = BallFlightKind.None,
            StartPos = Vector2.Zero,
            EndPos = Vector2.Zero,
            ElapsedSeconds = 0f,
            DurationSeconds = 0f,
            ApexHeight = 0f,
            Height = 0f,
            IsComplete = true,
        });

        Console.WriteLine($"[sim-arch] spawned demo scrimmage roster off={offense.Count} def={defense.Count} ballOwner={qbId} losY={losY}");
        return (offense, defense, ball.Id);
    }
}
