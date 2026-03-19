using System;
using Arch.Core;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.Components.PlayCall;
using TecmoSBGame.SimArch.Events;

namespace TecmoSBGame.SimArch.Systems.PlayCall;

/// <summary>
/// Publishes PlaySelectedEvent when the user confirms a play in PreSnap.
///
/// This keeps MainGame free of play-number hardcoding.
/// </summary>
public sealed class PlayCallPublishSelectionSystem
{
    public void Update(World world, in UiButtons ui)
    {
        if (!ui.Select)
            return;

        var q = new QueryDescription().WithAll<PlayCallState>();
        world.Query(in q, (Entity _, ref PlayCallState pcs) =>
        {
            if (string.IsNullOrWhiteSpace(pcs.SelectedFormationId) || pcs.SelectedPlay is null)
                return;

            // Pick play number 0 (leftmost) for now; later this will use defensive matchup index.
            var playNumber = pcs.SelectedPlay.PlayNumbers.Count > 0 ? pcs.SelectedPlay.PlayNumbers[0] : 0;

            var ev = new PlaySelectedEvent(
                OffensiveFormationId: pcs.SelectedFormationId,
                OffensivePlayName: pcs.SelectedPlay.Name,
                OffensivePlaySlot: pcs.SelectedPlay.Slot,
                OffensivePlayNumber: playNumber,
                DefensiveCallId: pcs.SelectedDefenseId);

            SimEventBus.Send(ref ev);
        });
    }
}
