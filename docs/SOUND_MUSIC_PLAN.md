# Sound & Music Plan

This document outlines the audio strategy for the Tecmo Super Bowl MonoGame remake.

## Overview

The NES version uses a custom sound engine (Bank 28) with DMC samples for voice clips (Bank 32). For the MonoGame remake, we'll use modern audio formats while preserving the classic feel.

## Audio Architecture

### Sound Effect Categories

1. **Gameplay SFX**
   - Whistle (play start/end)
   - Snap (ball hike)
   - Tackle/collision
   - Ball catch
   - Footsteps (running)
   - Crowd noise (ambient)

2. **UI SFX**
   - Menu navigation (up/down/select)
   - Play selection confirm
   - Pause/unpause

3. **Special Effects**
   - Touchdown celebration
   - Sack/grunt
   - Injured player

### Music Categories

1. **Title Screen** - Main theme (inspired by original)
2. **Menu Music** - Team selection, playbook
3. **Gameplay Music** - Optional, can be ambient crowd only
4. **Touchdown Music** - Short celebratory jingle
5. **Game Over** - Win/loss themes

## Implementation Strategy

### Option 1: Original Audio Extraction (Preferred for authenticity)

Extract original audio from NES ROM:
- Use NES APU emulation to record authentic sounds
- Convert DMC samples to WAV
- Record music tracks via NSF player

**Tools:**
- NSFPlayer + recorder
- DMCtoWAV converter
- Audio editing for cleanup

### Option 2: Modern Recreation

Recreate sounds using modern synthesis:
- Use similar waveforms (square, triangle, noise)
- Compose music in similar style
- Higher quality but less authentic

**Tools:**
- FMOD or Wwise for advanced audio
- MonoGame SoundEffect for simple SFX

## Technical Implementation

### MonoGame Audio Setup

```csharp
// SoundEffect for short SFX
SoundEffect whistleSfx;
SoundEffect tackleSfx;

// Song for music
Song titleMusic;
Song gameplayMusic;

// Load content
whistleSfx = Content.Load<SoundEffect>("Audio/sfx_whistle");
titleMusic = Content.Load<Song>("Audio/music_title");
```

### Audio Manager

Create a central audio manager:

```csharp
public class AudioManager
{
    public void PlaySfx(string name);
    public void PlayMusic(string name);
    public void StopMusic();
    public void SetSfxVolume(float volume);
    public void SetMusicVolume(float volume);
}
```

### Event-Driven Audio

Hook audio to game events:

```csharp
// In GameStateSystem
public event Action OnTouchdown;
public event Action OnTackle;

// In AudioManager
gameState.OnTouchdown += () => PlaySfx("touchdown");
gameState.OnTackle += () => PlaySfx("tackle");
```

## Asset Pipeline

### Source Files

```
assets/audio/
├── sfx/
│   ├── whistle.wav
│   ├── tackle.wav
│   ├── snap.wav
│   └── ...
└── music/
    ├── title.ogg
    ├── menu.ogg
    └── gameplay.ogg
```

### Content Pipeline

Add to Content.mgcb:

```
/importer:WavImporter
/processor:SoundEffectProcessor
/processorParam:Quality=Best
/build:Audio/sfx_whistle.wav

/importer:OggImporter
/processor:SongProcessor
/processorParam:Quality=Best
/build:Audio/music_title.ogg
```

## Volume Levels

| Category | Default | Range | Notes |
|----------|---------|-------|-------|
| Master | 1.0 | 0.0-1.0 | Overall volume |
| Music | 0.7 | 0.0-1.0 | Background music |
| SFX | 1.0 | 0.0-1.0 | Sound effects |
| Crowd | 0.5 | 0.0-1.0 | Ambient crowd |

## Priority Levels

1. **High** - Whistle, snap (critical gameplay cues)
2. **Medium** - Tackles, catches (important feedback)
3. **Low** - Footsteps, ambient (atmospheric)

## Next Steps

1. Extract/record original SFX from NES ROM
2. Create AudioManager class
3. Hook audio events into GameStateSystem
4. Add volume controls to settings menu
5. Test on target platforms (Linux/Windows/macOS)

## Open Questions

- Should we include the classic "Boo" and cheer samples?
- Music during gameplay or ambient crowd only?
- Voice synthesis for play names (audible)?
