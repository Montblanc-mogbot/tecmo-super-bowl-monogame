using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSB;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Spawning;

/// <summary>
/// Formation-driven roster spawning for SimArch.
///
/// Current scope:
/// - Uses FormationDataConfig (YAML) offensive formations to place the 11 offensive players.
/// - Uses the first SetPosFromKick/SetPosFromHike/SetPosFromMid occurrence in the command string
///   to derive an initial position.
/// - Spawns a simple placeholder defense (until defensive formation YAML is added).
/// </summary>
public static class FormationSpawner
{
    // NES-ish coordinate anchor defaults.
    private static readonly Vector2 DefaultKickoffAnchor = new(56, 112);
    private static readonly Vector2 DefaultHikeAnchor = new(128, 112);
    private static readonly Vector2 DefaultMidAnchor = new(128, 112);

    public static (List<int> offenseEntityIds, List<int> defenseEntityIds, int ballEntityId) SpawnScrimmage(
        World world,
        FormationDataConfig formationData,
        string? offenseFormationId = null,
        int offenseTeamIndex = 1,
        int defenseTeamIndex = 0)
    {
        if (world is null) throw new ArgumentNullException(nameof(world));
        if (formationData is null) throw new ArgumentNullException(nameof(formationData));

        if (formationData.OffensiveFormations is null || formationData.OffensiveFormations.Count == 0)
            return SpawnLegacyDemoScrimmage(world, offenseTeamIndex, defenseTeamIndex);

        var formationId = PickOffensiveFormationId(formationData, offenseFormationId);
        var formation = formationData.OffensiveFormations.First(f => f.Id == formationId);

        var offense = new List<int>(capacity: 11);
        var defense = new List<int>(capacity: 11);

        int SpawnPlayer(RoleId role, Vector2 pos, bool isOffense, int teamIndex, bool isPlayerControlled)
        {
            var e = world.Create();

            e.Add(new Position { Value = pos });
            e.Add(new Velocity { Value = Vector2.Zero });
            e.Add(new Team { TeamIndex = teamIndex, IsOffense = isOffense, IsPlayerControlled = isPlayerControlled });
            e.Add(new Role { Id = role });
            e.Add(new Ratings { MS = 50, HP = 50, RS = 50 });
            e.Add(new MovementTuning { MaxSpeedPerTick = 1.5f, MaxTurnDegreesPerTick = 9f, AccelPerTick = 0f, DecelPerTick = 0f });
            e.Add(new BehaviorStack { Count = 0 });
            e.Add(new Engagement { PartnerEntityId = -1, CooldownSeconds = 0f });

            // Only offense entities participate as blockers.
            if (isOffense)
            {
                e.Add(new BlockTarget { TargetEntityId = -1, Assignment = BlockAssignmentType.ManOn, IsEngaged = false, EngagedEntityId = -1, EngagementFrame = 0, IsDoubleTeam = false });
            }
            else
            {
                // Default defensive rush assignment + coverage (can be overridden later via YAML).
                e.Add(new Rush { Assignment = RushAssignment.AGapLeft, HasLandmark = false, Landmark = Vector2.Zero, ReachedLandmark = false });
                e.Add(new Coverage
                {
                    Type = CoverageType.ZoneHook,
                    AssignmentTargetId = -1,
                    Zone = ZoneLandmark.HookLeft,
                    LandmarkPosition = Vector2.Zero,
                    InPursuit = false,
                    PursuitTargetId = -1,
                    ReactionDelay = 0,
                    ReactionTimer = 0,
                    HasReacted = false,
                });
            }

            e.Add(new Behavior { State = BehaviorState.Idle, TargetEntityId = -1, TargetPosition = Vector2.Zero, StateTimer = 0f });

            return e.Id;
        }

        // Offense: spawn from YAML positions.
        var qbId = -1;
        var qbPos = DefaultHikeAnchor;

        foreach (var slot in formation.Players)
        {
            var role = MapPositionToRoleId(slot.Position);
            var pos = TryParseInitialPosition(slot.Commands, DefaultKickoffAnchor, DefaultHikeAnchor, DefaultMidAnchor)
                ?? FallbackPosition(role);

            var id = SpawnPlayer(role, pos, isOffense: true, teamIndex: offenseTeamIndex, isPlayerControlled: true);
            offense.Add(id);

            if (role == RoleId.QB)
            {
                qbId = id;
                qbPos = pos;
            }
        }

        // Defense: placeholder front 4 / LB 4 / DB 3.
        // (Uses offsets anchored around hike centerline so it looks reasonable versus scrimmage offense.)
        var origin = DefaultHikeAnchor;
        defense.Add(SpawnPlayer(RoleId.DL1, origin + new Vector2(-24, 16), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));
        defense.Add(SpawnPlayer(RoleId.DL2, origin + new Vector2(-8, 16), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));
        defense.Add(SpawnPlayer(RoleId.DL3, origin + new Vector2(8, 16), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));
        defense.Add(SpawnPlayer(RoleId.DL4, origin + new Vector2(24, 16), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));

        defense.Add(SpawnPlayer(RoleId.LB1, origin + new Vector2(-40, 32), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));
        defense.Add(SpawnPlayer(RoleId.LB2, origin + new Vector2(-16, 32), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));
        defense.Add(SpawnPlayer(RoleId.LB3, origin + new Vector2(16, 32), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));
        defense.Add(SpawnPlayer(RoleId.LB4, origin + new Vector2(40, 32), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));

        defense.Add(SpawnPlayer(RoleId.CB1, origin + new Vector2(-56, 56), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));
        defense.Add(SpawnPlayer(RoleId.CB2, origin + new Vector2(56, 56), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));
        defense.Add(SpawnPlayer(RoleId.S1, origin + new Vector2(0, 72), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));

        // Ball entity: start held by QB.
        if (qbId < 0)
        {
            qbId = offense.FirstOrDefault();
            qbPos = DefaultHikeAnchor;
        }

        var ball = world.Create();
        ball.Add(new Position { Value = qbPos });
        ball.Add(new Velocity { Value = Vector2.Zero });
        ball.Add(new Ball
        {
            State = Components.BallState.Held,
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

        Console.WriteLine($"[sim-arch] spawned formation scrimmage roster formation={formationId} off={offense.Count} def={defense.Count} ballOwner={qbId}");
        return (offense, defense, ball.Id);
    }

    /// <summary>
    /// Legacy deterministic demo roster. Prefer <see cref="SpawnScrimmage"/>.
    /// </summary>
    public static (List<int> offenseEntityIds, List<int> defenseEntityIds, int ballEntityId) SpawnDemoScrimmage(World world)
        => SpawnLegacyDemoScrimmage(world, offenseTeamIndex: 1, defenseTeamIndex: 0);

    private static string PickOffensiveFormationId(FormationDataConfig data, string? requested)
    {
        // Precondition: data has at least one formation (checked by caller).

        if (!string.IsNullOrWhiteSpace(requested) && data.OffensiveFormations.Any(f => f.Id == requested))
            return requested;

        // Prefer a "normal" scrimmage-ish formation if present; otherwise first.
        if (data.OffensiveFormations.Any(f => f.Id == "03"))
            return "03";
        if (data.OffensiveFormations.Any(f => f.Id == "01"))
            return "01";

        return data.OffensiveFormations.First().Id;
    }

    private static (List<int> offenseEntityIds, List<int> defenseEntityIds, int ballEntityId) SpawnLegacyDemoScrimmage(
        World world,
        int offenseTeamIndex,
        int defenseTeamIndex)
    {
        var offense = new List<int>(11);
        var defense = new List<int>(11);

        var origin = DefaultHikeAnchor;

        int SpawnPlayer(RoleId role, Vector2 offset, bool isOffense, int teamIndex, bool isPlayerControlled)
        {
            var e = world.Create();

            e.Add(new Position { Value = origin + offset });
            e.Add(new Velocity { Value = Vector2.Zero });
            e.Add(new Team { TeamIndex = teamIndex, IsOffense = isOffense, IsPlayerControlled = isPlayerControlled });
            e.Add(new Role { Id = role });
            e.Add(new Ratings { MS = 50, HP = 50, RS = 50 });
            e.Add(new MovementTuning { MaxSpeedPerTick = 1.5f, MaxTurnDegreesPerTick = 9f, AccelPerTick = 0f, DecelPerTick = 0f });
            e.Add(new BehaviorStack { Count = 0 });
            e.Add(new Engagement { PartnerEntityId = -1, CooldownSeconds = 0f });

            if (isOffense)
            {
                e.Add(new BlockTarget { TargetEntityId = -1, Assignment = BlockAssignmentType.ManOn, IsEngaged = false, EngagedEntityId = -1, EngagementFrame = 0, IsDoubleTeam = false });
            }
            else
            {
                e.Add(new Rush { Assignment = RushAssignment.AGapLeft, HasLandmark = false, Landmark = Vector2.Zero, ReachedLandmark = false });
                e.Add(new Coverage
                {
                    Type = CoverageType.ZoneHook,
                    AssignmentTargetId = -1,
                    Zone = ZoneLandmark.HookLeft,
                    LandmarkPosition = Vector2.Zero,
                    InPursuit = false,
                    PursuitTargetId = -1,
                    ReactionDelay = 0,
                    ReactionTimer = 0,
                    HasReacted = false,
                });
            }

            e.Add(new Behavior { State = BehaviorState.Idle, TargetEntityId = -1, TargetPosition = Vector2.Zero, StateTimer = 0f });

            return e.Id;
        }

        // QB/HB/FB
        offense.Add(SpawnPlayer(RoleId.QB, new Vector2(0, -12), isOffense: true, teamIndex: offenseTeamIndex, isPlayerControlled: true));
        offense.Add(SpawnPlayer(RoleId.HB, new Vector2(16, -4), isOffense: true, teamIndex: offenseTeamIndex, isPlayerControlled: true));
        offense.Add(SpawnPlayer(RoleId.FB, new Vector2(-16, -4), isOffense: true, teamIndex: offenseTeamIndex, isPlayerControlled: true));

        // WR/TE
        offense.Add(SpawnPlayer(RoleId.WR1, new Vector2(-64, -20), isOffense: true, teamIndex: offenseTeamIndex, isPlayerControlled: true));
        offense.Add(SpawnPlayer(RoleId.WR2, new Vector2(64, -20), isOffense: true, teamIndex: offenseTeamIndex, isPlayerControlled: true));
        offense.Add(SpawnPlayer(RoleId.TE, new Vector2(24, -8), isOffense: true, teamIndex: offenseTeamIndex, isPlayerControlled: true));

        // OL
        offense.Add(SpawnPlayer(RoleId.OC, new Vector2(0, 0), isOffense: true, teamIndex: offenseTeamIndex, isPlayerControlled: true));
        offense.Add(SpawnPlayer(RoleId.LG, new Vector2(-16, 0), isOffense: true, teamIndex: offenseTeamIndex, isPlayerControlled: true));
        offense.Add(SpawnPlayer(RoleId.RG, new Vector2(16, 0), isOffense: true, teamIndex: offenseTeamIndex, isPlayerControlled: true));
        offense.Add(SpawnPlayer(RoleId.LT, new Vector2(-32, 0), isOffense: true, teamIndex: offenseTeamIndex, isPlayerControlled: true));
        offense.Add(SpawnPlayer(RoleId.RT, new Vector2(32, 0), isOffense: true, teamIndex: offenseTeamIndex, isPlayerControlled: true));

        // Defense (simple front 4 / LB 4 / DB 3)
        defense.Add(SpawnPlayer(RoleId.DL1, new Vector2(-24, 16), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));
        defense.Add(SpawnPlayer(RoleId.DL2, new Vector2(-8, 16), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));
        defense.Add(SpawnPlayer(RoleId.DL3, new Vector2(8, 16), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));
        defense.Add(SpawnPlayer(RoleId.DL4, new Vector2(24, 16), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));

        defense.Add(SpawnPlayer(RoleId.LB1, new Vector2(-40, 32), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));
        defense.Add(SpawnPlayer(RoleId.LB2, new Vector2(-16, 32), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));
        defense.Add(SpawnPlayer(RoleId.LB3, new Vector2(16, 32), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));
        defense.Add(SpawnPlayer(RoleId.LB4, new Vector2(40, 32), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));

        defense.Add(SpawnPlayer(RoleId.CB1, new Vector2(-56, 56), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));
        defense.Add(SpawnPlayer(RoleId.CB2, new Vector2(56, 56), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));
        defense.Add(SpawnPlayer(RoleId.S1, new Vector2(0, 72), isOffense: false, teamIndex: defenseTeamIndex, isPlayerControlled: false));

        // Ball entity: start held by QB
        var qbId = offense[0];
        var ball = world.Create();
        ball.Add(new Position { Value = origin + new Vector2(0, -12) });
        ball.Add(new Velocity { Value = Vector2.Zero });
        ball.Add(new Ball
        {
            State = Components.BallState.Held,
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

        Console.WriteLine($"[sim-arch] spawned legacy demo scrimmage roster off={offense.Count} def={defense.Count} ballOwner={qbId}");
        return (offense, defense, ball.Id);
    }

    private static RoleId MapPositionToRoleId(string position)
    {
        var p = (position ?? string.Empty).Trim().ToUpperInvariant();
        return p switch
        {
            "QB" => RoleId.QB,
            "HB" => RoleId.HB,
            "FB" => RoleId.FB,
            "WR1" => RoleId.WR1,
            "WR2" => RoleId.WR2,
            "TE" => RoleId.TE,
            "OC" => RoleId.OC,
            "LG" => RoleId.LG,
            "RG" => RoleId.RG,
            "LT" => RoleId.LT,
            "RT" => RoleId.RT,
            _ => RoleId.Unknown,
        };
    }

    private static Vector2 FallbackPosition(RoleId role)
    {
        // Rough fallback around hike anchor.
        var o = DefaultHikeAnchor;
        return role switch
        {
            RoleId.QB => o + new Vector2(0, -12),
            RoleId.HB => o + new Vector2(16, -4),
            RoleId.FB => o + new Vector2(-16, -4),
            RoleId.WR1 => o + new Vector2(-64, -20),
            RoleId.WR2 => o + new Vector2(64, -20),
            RoleId.TE => o + new Vector2(24, -8),
            RoleId.OC => o + new Vector2(0, 0),
            RoleId.LG => o + new Vector2(-16, 0),
            RoleId.RG => o + new Vector2(16, 0),
            RoleId.LT => o + new Vector2(-32, 0),
            RoleId.RT => o + new Vector2(32, 0),
            _ => o,
        };
    }

    private static Vector2? TryParseInitialPosition(string commands, Vector2 kickoffAnchor, Vector2 hikeAnchor, Vector2 midAnchor)
    {
        if (string.IsNullOrWhiteSpace(commands))
            return null;

        // Example: "B0-SetPosFromKick(F0 80);" or "SetPosFromHike(F0 48);"
        if (TryParseCommandBytes(commands, "SetPosFromKick", out var xKick, out var yKick))
            return DecodePosition(xKick, yKick, kickoffAnchor);

        if (TryParseCommandBytes(commands, "SetPosFromHike", out var xHike, out var yHike))
            return DecodePosition(xHike, yHike, hikeAnchor);

        if (TryParseCommandBytes(commands, "SetPosFromMid", out var xMid, out var yMid))
            return DecodePosition(xMid, yMid, midAnchor);

        return null;
    }

    private static Vector2 DecodePosition(byte xByte, byte yByte, Vector2 anchor)
    {
        // Heuristic decoding:
        // - X is treated as a signed byte offset from the anchor.
        // - Y is treated as an unsigned byte with 0x80 meaning "center line".
        var x = anchor.X + unchecked((sbyte)xByte);
        var y = anchor.Y + (yByte - 0x80);
        return new Vector2(x, y);
    }

    private static bool TryParseCommandBytes(string commands, string commandName, out byte x, out byte y)
    {
        x = 0;
        y = 0;

        var pattern = $@"{Regex.Escape(commandName)}\((?<x>[0-9A-Fa-f]{{2}})\s+(?<y>[0-9A-Fa-f]{{2}})\)";
        var m = Regex.Match(commands, pattern);
        if (!m.Success)
            return false;

        if (!byte.TryParse(m.Groups["x"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out x))
            return false;
        if (!byte.TryParse(m.Groups["y"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out y))
            return false;

        return true;
    }
}
