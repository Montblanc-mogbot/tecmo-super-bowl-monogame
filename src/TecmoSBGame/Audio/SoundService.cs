using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;

namespace TecmoSBGame.Audio;

/// <summary>
/// Minimal sound facade.
///
/// - Loads SoundEffects by Content key (optional; failures are tolerated)
/// - Plays 1-shot cues
///
/// This is a scaffold until we have ROM-authentic cue timing + mixing.
/// </summary>
public sealed class SoundService
{
    private readonly ContentManager _content;
    private readonly Dictionary<SoundCue, SoundEffect> _effects = new();

    public bool Enabled { get; set; } = true;
    public float Volume { get; set; } = 0.85f;

    public MusicState MusicState { get; private set; } = MusicState.None;

    public SoundService(ContentManager content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public void Register(SoundCue cue, string contentKey)
    {
        if (string.IsNullOrWhiteSpace(contentKey))
            return;

        try
        {
            var effect = _content.Load<SoundEffect>(contentKey);
            _effects[cue] = effect;
        }
        catch
        {
            // Missing content is OK in scaffold mode.
        }
    }

    public void LoadDefaultCues()
    {
        // Placeholder keys; add real .xnb assets later.
        Register(SoundCue.Snap, "audio/snap");
        Register(SoundCue.Catch, "audio/catch");
        Register(SoundCue.Interception, "audio/interception");
        Register(SoundCue.Incomplete, "audio/incomplete");
        Register(SoundCue.Hit, "audio/hit");
        Register(SoundCue.Whistle, "audio/whistle");
        Register(SoundCue.Fumble, "audio/fumble");
        Register(SoundCue.Crowd, "audio/crowd");
        Register(SoundCue.MenuMove, "audio/menu_move");
        Register(SoundCue.MenuSelect, "audio/menu_select");
    }

    public void Play(SoundCue cue, float? volume = null, float pitch = 0f, float pan = 0f)
    {
        if (!Enabled)
            return;

        if (!_effects.TryGetValue(cue, out var fx))
            return;

        fx.Play(volume ?? Volume, pitch, pan);
    }

    public void SetMusicState(MusicState state)
    {
        if (MusicState == state)
            return;

        MusicState = state;

        // TODO: implement actual music playback via Song/MediaPlayer and content keys.
        // For now this is a state machine only.
    }
}
