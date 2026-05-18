using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;

namespace TecmoSBGame.Audio;

/// <summary>
/// Minimal sound facade.
///
/// - Loads SoundEffects by Content key
/// - Plays 1-shot cues
/// - Tracks missing cues explicitly so silence is never implicit
///
/// This is a scaffold until we have ROM-authentic cue timing + mixing.
/// </summary>
public sealed class SoundService
{
    private readonly ContentManager _content;
    private readonly Dictionary<SoundCue, SoundEffect> _effects = new();
    private readonly HashSet<SoundCue> _missingCues = new();

    public bool Enabled { get; set; } = true;
    public float Volume { get; set; } = 0.85f;

    public MusicState MusicState { get; private set; } = MusicState.None;
    public IReadOnlyCollection<SoundCue> MissingCues => _missingCues;

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
            _missingCues.Add(cue);
        }
    }

    public void LoadDefaultCues()
    {
        Register(SoundCue.Snap, "Audio/snap");
        Register(SoundCue.Whistle, "Audio/whistle");
        Register(SoundCue.Tackle, "Audio/tackle");
        Register(SoundCue.Incomplete, "Audio/incomplete");
        Register(SoundCue.Turnover, "Audio/turnover");
        Register(SoundCue.Touchdown, "Audio/touchdown");
        Register(SoundCue.MenuMove, "Audio/menu_move");
        Register(SoundCue.MenuSelect, "Audio/menu_select");
    }

    public void Play(SoundCue cue, float? volume = null, float pitch = 0f, float pan = 0f)
    {
        if (!Enabled)
            return;

        if (!_effects.TryGetValue(cue, out var fx))
        {
            _missingCues.Add(cue);
            return;
        }

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

    public string DescribeMissingCues()
    {
        if (_missingCues.Count == 0)
            return "none";

        return string.Join(", ", _missingCues.OrderBy(c => c).Select(c => c.ToString()));
    }
}
