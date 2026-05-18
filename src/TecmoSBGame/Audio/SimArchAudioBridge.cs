using System;
using TecmoSBGame.SimArch.Events;
using TecmoSBGame.SimArch.State;

namespace TecmoSBGame.Audio;

/// <summary>
/// Non-deterministic bridge from SimArch events/state transitions to runtime audio cues.
/// </summary>
public sealed class SimArchAudioBridge
{
    private readonly SoundService _sound;
    private int _lastPlaySelectionHash;
    private int _lastWhistledPlayId = -1;
    private int _lastTouchdownPlayId = -1;
    private int _lastTurnoverPlayId = -1;

    public SimArchAudioBridge(SoundService sound)
    {
        _sound = sound ?? throw new ArgumentNullException(nameof(sound));
    }

    public void Update(PlayState play)
    {
        foreach (var _ in SimEventBus.Drain<SnapEvent>())
            _sound.Play(SoundCue.Snap);

        foreach (var selected in SimEventBus.Drain<PlaySelectedEvent>())
        {
            var hash = HashCode.Combine(selected.OffensiveFormationId, selected.OffensivePlayName, selected.OffensivePlayNumber, selected.DefensiveCallId);
            if (hash != _lastPlaySelectionHash)
            {
                _sound.Play(SoundCue.MenuSelect, volume: 0.75f);
                _lastPlaySelectionHash = hash;
            }
        }

        foreach (var whistle in SimEventBus.Drain<WhistleEvent>())
        {
            if (play.PlayId == _lastWhistledPlayId)
                continue;

            switch (whistle.Reason.ToLowerInvariant())
            {
                case "incomplete":
                    _sound.Play(SoundCue.Incomplete);
                    break;
                default:
                    _sound.Play(SoundCue.Whistle);
                    break;
            }

            _lastWhistledPlayId = play.PlayId;
        }

        foreach (var ended in SimEventBus.Drain<PlayEndedEvent>())
        {
            if ((WhistleReason)ended.Reason == WhistleReason.Tackle)
                _sound.Play(SoundCue.Tackle);

            if (ended.Touchdown && ended.PlayId != _lastTouchdownPlayId)
            {
                _sound.Play(SoundCue.Touchdown);
                _lastTouchdownPlayId = ended.PlayId;
            }

            if (ended.Turnover && ended.PlayId != _lastTurnoverPlayId)
            {
                _sound.Play(SoundCue.Turnover);
                _lastTurnoverPlayId = ended.PlayId;
            }
        }
    }

    public void OnMenuMove()
    {
        _sound.Play(SoundCue.MenuMove, volume: 0.55f);
    }

    public void OnMenuSelect()
    {
        _sound.Play(SoundCue.MenuSelect, volume: 0.75f);
    }
}
