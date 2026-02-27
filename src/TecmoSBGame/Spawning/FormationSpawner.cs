using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using TecmoSB;
using TecmoSBGame.Components;
using TecmoSBGame.Factories;

namespace TecmoSBGame.Spawning;

/// <summary>
/// Turns YAML formation entries (FormationDataConfig) into spawned player entities.
///
/// Current scope:
/// - Uses the first SetPosFromKick/SetPosFromHike/SetPosFromMid occurrence in the command string
///   to derive an initial position.
/// - Assigns a standardized PlayerRoleComponent.
/// - Uses a deterministic placeholder TeamRoster until real roster data exists.
///
/// This is intentionally a small service/factory so kickoff and headless can share it.
/// </summary>
public sealed class FormationSpawner
{
    public sealed record SpawnedPlayer(
        int EntityId,
        string Slot,
        PlayerRole Role,
        Vector2 Position);

    public sealed record SpawnedFormation(
        string FormationId,
        IReadOnlyList<SpawnedPlayer> Players);

    // NES-ish coordinate anchor defaults.
    private static readonly Vector2 DefaultKickoffAnchor = new(56, 112);
    private static readonly Vector2 DefaultHikeAnchor = new(128, 112);
    private static readonly Vector2 DefaultMidAnchor = new(128, 112);

    public SpawnedFormation Spawn(
        World world,
        FormationDataConfig formationData,
        string formationId,
        int teamIndex,
        bool isOffense,
        bool playerControlled,
        TeamRoster? roster = null,
        Vector2? kickoffAnchor = null,
        Vector2? hikeAnchor = null,
        Vector2? midAnchor = null)
    {
        if (world is null) throw new ArgumentNullException(nameof(world));
        if (formationData is null) throw new ArgumentNullException(nameof(formationData));
        if (string.IsNullOrWhiteSpace(formationId)) throw new ArgumentException("formationId is required", nameof(formationId));

        var formation = formationData.OffensiveFormations.FirstOrDefault(f => f.Id == formationId);
        if (formation is null)
            throw new InvalidOperationException($"Formation id '{formationId}' not found in YAML.");

        roster ??= new TeamRoster(teamId: $"T{teamIndex}");

        var kAnchor = kickoffAnchor ?? DefaultKickoffAnchor;
        var hAnchor = hikeAnchor ?? DefaultHikeAnchor;
        var mAnchor = midAnchor ?? DefaultMidAnchor;

        var spawned = new List<SpawnedPlayer>(formation.Players.Count);

        foreach (var slot in formation.Players)
        {
            var roleKey = MapSlotToRoleKey(slot.Position, slot.Commands, formation.Description);
            var rosterPlayer = roster.Next(roleKey);

            var initialPos = TryParseInitialPosition(slot.Commands, kAnchor, hAnchor, mAnchor)
                ?? FallbackPosition(roleKey, slot.Position);

            // Player-controlled: keep existing kickoff behavior: control the "main" actor.
            // In kickoff, YAML "00" has an RT entry that appears to be the controlled kicker.
            // We'll mark the first K/P/QB as player-controlled, otherwise none.
            var isControlled = playerControlled && roleKey is PlayerRoleKey.K or PlayerRoleKey.P;

            var entityId = PlayerEntityFactory.CreatePlayerWithAttributes(
                world,
                initialPos,
                teamIndex,
                isPlayerControlled: isControlled,
                isOffense: isOffense,
                positionName: slot.Position,
                playerName: rosterPlayer.Name,
                jerseyNumber: rosterPlayer.JerseyNumber,
                stats: rosterPlayer.Stats);

            var entity = world.GetEntity(entityId);
            entity.Attach(new PlayerRoleComponent(MapRoleKeyToComponentRole(roleKey), slot.Position));

            spawned.Add(new SpawnedPlayer(entityId, slot.Position, MapRoleKeyToComponentRole(roleKey), initialPos));
        }

        return new SpawnedFormation(formationId, spawned);
    }

    private static PlayerRole MapRoleKeyToComponentRole(PlayerRoleKey key) => key switch
    {
        PlayerRoleKey.QB => PlayerRole.QB,
        PlayerRoleKey.RB => PlayerRole.RB,
        PlayerRoleKey.WR => PlayerRole.WR,
        PlayerRoleKey.TE => PlayerRole.TE,
        PlayerRoleKey.OL => PlayerRole.OL,
        PlayerRoleKey.DL => PlayerRole.DL,
        PlayerRoleKey.LB => PlayerRole.LB,
        PlayerRoleKey.DB => PlayerRole.DB,
        PlayerRoleKey.K => PlayerRole.K,
        PlayerRoleKey.P => PlayerRole.P,
        _ => PlayerRole.Unknown,
    };

    /// <summary>
    /// Maps a YAML formation slot key (QB/HB/WR1/OC/LG/etc) to a standardized role.
    ///
    /// Heuristic overrides:
    /// - If commands contain Punt => P
    /// - If commands contain FieldGoal/ExtraPoint => K
    /// </summary>
    public static PlayerRoleKey MapSlotToRoleKey(string slot, string commands, string formationDescription)
    {
        var s = (slot ?? string.Empty).Trim().ToUpperInvariant();
        var c = commands ?? string.Empty;
        var d = formationDescription ?? string.Empty;

        if (c.Contains("Punt", StringComparison.OrdinalIgnoreCase) || d.Contains("Punt", StringComparison.OrdinalIgnoreCase))
            return PlayerRoleKey.P;

        if (c.Contains("FieldGoal", StringComparison.OrdinalIgnoreCase)
            || c.Contains("ExtraPoint", StringComparison.OrdinalIgnoreCase)
            || d.Contains("Field Goal", StringComparison.OrdinalIgnoreCase)
            || d.Contains("Extra Point", StringComparison.OrdinalIgnoreCase))
            return PlayerRoleKey.K;

        return s switch
        {
            "QB" => PlayerRoleKey.QB,
            "HB" => PlayerRoleKey.RB,
            "FB" => PlayerRoleKey.RB,
            "WR" or "WR1" or "WR2" or "WR3" => PlayerRoleKey.WR,
            "TE" or "TE1" or "TE2" => PlayerRoleKey.TE,

            // Offensive line slots in current YAML
            "OC" or "C" or "LG" or "RG" or "LT" or "RT" or "G" or "T" or "OL" => PlayerRoleKey.OL,

            // Defensive placeholders (not currently in YAML, but defined for future)
            "DL" or "DE" or "DT" or "NT" => PlayerRoleKey.DL,
            "LB" or "MLB" or "OLB" or "ILB" => PlayerRoleKey.LB,
            "DB" or "CB" or "S" or "FS" or "SS" => PlayerRoleKey.DB,

            "K" => PlayerRoleKey.K,
            "P" => PlayerRoleKey.P,

            _ => PlayerRoleKey.Unknown,
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
        // This matches many NES-era coordinate encodings and yields reasonable on-field values
        // for the current YAML sample.
        var x = anchor.X + unchecked((sbyte)xByte);
        var y = anchor.Y + (yByte - 0x80);
        return new Vector2(x, y);
    }

    private static bool TryParseCommandBytes(string commands, string commandName, out byte x, out byte y)
    {
        x = 0;
        y = 0;

        // Find first occurrence of "CommandName(AA BB)" where AA/BB are hex bytes.
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

    private static Vector2 FallbackPosition(PlayerRoleKey role, string slot)
    {
        // Stable fallback positions near center. Only used if YAML parsing fails.
        // Keep it deterministic and distinct by role/slot.
        var baseX = 128f;
        var baseY = 112f;

        var dy = role switch
        {
            PlayerRoleKey.WR => -40,
            PlayerRoleKey.TE => 25,
            PlayerRoleKey.RB => 15,
            PlayerRoleKey.OL => 0,
            _ => 0,
        };

        var hash = (slot ?? string.Empty).GetHashCode(StringComparison.Ordinal);
        var jitter = (hash % 9) - 4; // -4..+4

        return new Vector2(baseX + jitter, baseY + dy);
    }
}
