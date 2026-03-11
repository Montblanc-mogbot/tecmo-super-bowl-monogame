using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using TecmoSB;
using TecmoSBGame.Components;
using TecmoSBGame.Factories;
using TecmoSBGame.Spawning;

namespace TecmoSBGame.Systems;

public partial class GameStateSystem
{
    private void SpawnTestScrimmage(World world)
    {
        if (_formationData is null || _formationSpawner is null)
        {
            Console.WriteLine("[test-play] ERROR: FormationData/FormationSpawner not available; cannot bootstrap scrimmage.");
            return;
        }

        Console.WriteLine("[test-play] bootstrapping scrimmage setup (one offense play + one defense play)");

        // Hide kickoff entities (keep the dedicated ball entity alive).
        // We avoid World entity destruction APIs to keep this simple.
        foreach (var id in _kickingEntityIds)
        {
            if (_positionMapper.Has(id))
                _positionMapper.Get(id).Position = new Vector2(-10000, -10000);
        }

        foreach (var id in _receivingEntityIds)
        {
            if (_positionMapper.Has(id))
                _positionMapper.Get(id).Position = new Vector2(-10000, -10000);
        }

        _kickingEntityIds.Clear();
        _receivingEntityIds.Clear();

        var offenseTeam = _matchState.PossessionTeam;
        var defenseTeam = offenseTeam == 0 ? 1 : 0;

        // Prefer formation 01 if present (00 is kickoff).
        var formationId = _formationData.OffensiveFormations.Any(f => f.Id == "01")
            ? "01"
            : _formationData.OffensiveFormations.First().Id;

        var offense = _formationSpawner.Spawn(
            world,
            _formationData,
            formationId: formationId,
            teamIndex: offenseTeam,
            isOffense: true,
            playerControlled: true);

        var defenseIds = SpawnPlaceholderDefense(world, defenseTeam);

        // Create a deterministic test play + a deterministic defensive execution.
        // (PlaySpawner already generates simple routes + coverage responsibilities.)
        var testOffPlay = new PlayEntry(
            Name: "TEST_PASS",
            Slot: "Pass",
            Formation: formationId,
            PlayNumbers: new[] { 0 },
            Defense: Array.Empty<string>());

        var defId = _defensePlays?.DefensiveExecutions.FirstOrDefault()?.Id ?? "DEFENSIVE_EXECUTION_1";

        var playSpawner = new PlaySpawner();
        var spawned = playSpawner.Spawn(
            world,
            playList: _playList ?? StubPlayList(),
            defensePlays: _defensePlays ?? StubDefensePlays(),
            offenseEntityIds: offense.Players.Select(p => p.EntityId).ToList(),
            defenseEntityIds: defenseIds,
            selectedOffensivePlay: testOffPlay,
            selectedDefensiveCallId: defId);

        Console.WriteLine($"[test-play] formation={formationId} defenseCall={spawned.DefensiveCallId} assignments={spawned.Assignments.Count}");

        // Scrimmage play: allow passing.
        _playState.AllowPass = true;
    }

    private List<int> SpawnPlaceholderDefense(World world, int teamIndex)
    {
        var ids = new List<int>(capacity: 11);

        // DL (4)
        ids.Add(SpawnDef(world, new Vector2(0, 72), teamIndex, PlayerRole.DL, "DE-L"));
        ids.Add(SpawnDef(world, new Vector2(0, 96), teamIndex, PlayerRole.DL, "DT-L"));
        ids.Add(SpawnDef(world, new Vector2(0, 128), teamIndex, PlayerRole.DL, "DT-R"));
        ids.Add(SpawnDef(world, new Vector2(0, 152), teamIndex, PlayerRole.DL, "DE-R"));

        // LB (3)
        ids.Add(SpawnDef(world, new Vector2(-10, 84), teamIndex, PlayerRole.LB, "LB-L"));
        ids.Add(SpawnDef(world, new Vector2(-10, 112), teamIndex, PlayerRole.LB, "MLB"));
        ids.Add(SpawnDef(world, new Vector2(-10, 140), teamIndex, PlayerRole.LB, "LB-R"));

        // DB (4)
        ids.Add(SpawnDef(world, new Vector2(-22, 64), teamIndex, PlayerRole.DB, "CB-L"));
        ids.Add(SpawnDef(world, new Vector2(-22, 160), teamIndex, PlayerRole.DB, "CB-R"));
        ids.Add(SpawnDef(world, new Vector2(-30, 96), teamIndex, PlayerRole.DB, "S-L"));
        ids.Add(SpawnDef(world, new Vector2(-30, 128), teamIndex, PlayerRole.DB, "S-R"));

        return ids;
    }

    private static int SpawnDef(World world, Vector2 pos, int teamIndex, PlayerRole role, string slot)
    {
        var id = PlayerEntityFactory.CreatePlayer(world, pos, teamIndex, isPlayerControlled: false, isOffense: false, spriteId: "player_defense");
        world.GetEntity(id).Attach(new PlayerRoleComponent(role, slot));
        return id;
    }

    // These helper methods are placeholders until GameStateSystem is reworked into a full match orchestrator.
    private static PlayListConfig StubPlayList()
        => new(
            PlayList: Array.Empty<PlayEntry>(),
            Slots: Array.Empty<SlotDefinition>(),
            Notes: Array.Empty<string>());

    private static DefensePlayConfig StubDefensePlays()
        => new(
            Id: "stub",
            DefensiveExecutions: Array.Empty<DefensiveExecution>(),
            DefensePlayerReactions: Array.Empty<DefensePlayerReaction>(),
            SpecialTeamsExecutions: Array.Empty<SpecialTeamsExecution>(),
            RomInfo: new DefenseRomInfo(BaseAddress: 0, DefensePointersStart: 0, NumDefensiveExecutions: 0),
            Notes: Array.Empty<string>());
}
