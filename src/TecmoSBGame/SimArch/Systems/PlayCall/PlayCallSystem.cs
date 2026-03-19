using System;
using Arch.Core;
using Arch.Core.Extensions;
using TecmoSB;
using TecmoSBGame.SimArch.Components.PlayCall;

namespace TecmoSBGame.SimArch.Systems.PlayCall;

/// <summary>
/// Playcall selection system (formation/play/defense).
///
/// MVP scope:
/// - maintain a single index over PlayList entries
/// - publish selection details when requested by UiButtons.Select
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/PlayCall/PlayCallSystem.cs
/// </summary>
public sealed class PlayCallSystem
{
    private readonly FormationDataConfig _formations;
    private readonly PlayListConfig _playList;
    private readonly DefensePlayConfig _defensePlays;

    public PlayCallSystem(FormationDataConfig formations, PlayListConfig playList, DefensePlayConfig defensePlays)
    {
        _formations = formations ?? throw new ArgumentNullException(nameof(formations));
        _playList = playList ?? throw new ArgumentNullException(nameof(playList));
        _defensePlays = defensePlays ?? throw new ArgumentNullException(nameof(defensePlays));
    }

    public void Update(World world, TecmoSBGame.SimArch.Components.UiButtons ui)
    {
        // Can't capture an 'in' parameter in the query lambda; copy into locals.
        var up = ui.Up;
        var down = ui.Down;

        // Find the singleton PlayCallState
        var found = false;
        var q = new QueryDescription().WithAll<PlayCallState>();
        world.Query(in q, (Entity _, ref PlayCallState pcs) =>
        {
            found = true;

            if (pcs.FormationIds.Count == 0)
            {
                // initialize with unique formation ids from playlist
                var set = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in _playList.PlayList)
                    set.Add(entry.Formation);
                pcs.FormationIds.AddRange(set);
                pcs.FormationIds.Sort(StringComparer.OrdinalIgnoreCase);

                pcs.FormationIndex = 0;
                pcs.PlayIndex = 0;
                pcs.DefenseIndex = 0;
            }

            // Simple navigation: up/down cycles plays within the current formation.
            if (up) pcs.PlayIndex = Math.Max(0, pcs.PlayIndex - 1);
            if (down) pcs.PlayIndex = pcs.PlayIndex + 1;

            // Clamp play index within formation subset.
            var formationId = pcs.FormationIds[Math.Clamp(pcs.FormationIndex, 0, pcs.FormationIds.Count - 1)];
            pcs.SelectedFormationId = formationId;

            pcs.PlaysForFormation.Clear();
            foreach (var entry in _playList.PlayList)
            {
                if (string.Equals(entry.Formation, formationId, StringComparison.OrdinalIgnoreCase))
                    pcs.PlaysForFormation.Add(entry);
            }

            if (pcs.PlaysForFormation.Count == 0)
            {
                pcs.PlayIndex = 0;
                pcs.SelectedPlay = null;
                return;
            }

            pcs.PlayIndex = Math.Clamp(pcs.PlayIndex, 0, pcs.PlaysForFormation.Count - 1);
            pcs.SelectedPlay = pcs.PlaysForFormation[pcs.PlayIndex];

            // Minimal defense: choose first defensive execution.
            pcs.DefensiveCalls.Clear();
            foreach (var d in _defensePlays.DefensiveExecutions)
                pcs.DefensiveCalls.Add(d);
            pcs.DefenseIndex = Math.Clamp(pcs.DefenseIndex, 0, Math.Max(0, pcs.DefensiveCalls.Count - 1));

            pcs.SelectedDefenseId = pcs.DefensiveCalls.Count > 0 ? pcs.DefensiveCalls[pcs.DefenseIndex].Id : string.Empty;
        });

        if (!found)
        {
            // Create singleton if missing
            var e = world.Create();
            e.Add<PlayCallState>(PlayCallState.Default);
        }
    }
}
