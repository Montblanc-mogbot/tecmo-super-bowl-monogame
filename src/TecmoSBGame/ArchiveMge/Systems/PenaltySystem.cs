using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Deterministic penalty detection/enforcement scaffold.
///
/// IMPORTANT:
/// - When <see cref="MatchState.PenaltyRuleset"/> is <see cref="PenaltyRuleset.Off"/>, this system must be a no-op.
/// - When enabled (Basic), this system may emit <see cref="PenaltyEvent"/> and/or <see cref="PenaltyAssessedEvent"/>
///   in future work, but should remain deterministic.
/// </summary>
public sealed class PenaltySystem : UpdateSystem
{
    private readonly GameEvents? _events;
    private readonly MatchState _match;
    private readonly PlayState _play;

    // Track per-play processing to avoid emitting duplicates.
    private int _lastProcessedPlayId = -1;

    public PenaltySystem(GameEvents? events, MatchState matchState, PlayState playState)
    {
        _events = events;
        _match = matchState;
        _play = playState;
    }

    public override void Update(GameTime gameTime)
    {
        if (_events is null)
            return;

        if (_match.PenaltyRuleset == PenaltyRuleset.Off)
            return; // hard no-op by design.

        // ---- Basic ruleset scaffold ----
        // For now, do not change gameplay. This is only a placeholder so we can wire the system
        // into both runtime + headless without affecting behavior.

        // Avoid re-processing the same play multiple ticks.
        if (_play.PlayId == _lastProcessedPlayId)
            return;

        // In future work, we can key off SnapEvent (pre-snap penalties) or contacts (during play).
        // Example placeholder access (unused for now):
        // var snaps = _events.Read<SnapEvent>();

        _lastProcessedPlayId = _play.PlayId;
    }
}
