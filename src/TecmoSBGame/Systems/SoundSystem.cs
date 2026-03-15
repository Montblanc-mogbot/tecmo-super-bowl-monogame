using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Audio;
using TecmoSBGame.Events;

namespace TecmoSBGame.Systems;

/// <summary>
/// Drains gameplay events and triggers audio cues.
///
/// Sound is deliberately kept out of deterministic simulation logic; this is a side-effect system.
/// </summary>
public sealed class SoundSystem : UpdateSystem
{
    private readonly GameEvents _events;
    private readonly SoundService _sound;

    public SoundSystem(GameEvents events, SoundService sound)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _sound = sound ?? throw new ArgumentNullException(nameof(sound));
    }

    public override void Update(GameTime gameTime)
    {
        _events.Drain<SnapEvent>(_ => _sound.Play(SoundCue.Snap));
        _events.Drain<BallCaughtEvent>(_ => _sound.Play(SoundCue.Catch));

        _events.Drain<PassResolvedEvent>(e =>
        {
            switch (e.Outcome)
            {
                case PassOutcome.Catch:
                    _sound.Play(SoundCue.Catch);
                    break;
                case PassOutcome.Interception:
                    _sound.Play(SoundCue.Interception);
                    break;
                case PassOutcome.Incomplete:
                    _sound.Play(SoundCue.Incomplete);
                    break;
            }
        });

        _events.Drain<TackleEvent>(_ => _sound.Play(SoundCue.Hit));
        _events.Drain<BlockContactEvent>(_ => _sound.Play(SoundCue.Hit, volume: 0.35f));
        _events.Drain<WhistleEvent>(_ => _sound.Play(SoundCue.Whistle));
        _events.Drain<FumbleEvent>(_ => _sound.Play(SoundCue.Fumble));

        // UI/menu hooks can be added once we have menu event types.
    }
}
