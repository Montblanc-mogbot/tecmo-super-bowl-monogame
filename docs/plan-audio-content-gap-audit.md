# Audio & Content Gap Audit

Updated: 2026-05-07

## Scope

Audit the current MonoGame/SimArch audio hookup and content asset state against:
- `docs/FULL_COMPLETION_PLAN.md` phase 6 goals
- `docs/SOUND_MUSIC_PLAN.md`
- original disassembly audio banks:
  - `Bank28_sound_engine.asm`
  - `Bank29_sound_data.asm`
  - `Bank30_sound_data.asm`
  - `Bank32_DMC_Samples_reset_vector.asm`
  - `constants_variables/sound_ids.asm`

This document focuses on the highest-value parity work that remains explicit and actionable.

## Current state summary

### What exists today

1. **Audio scaffolding exists in code and YAML**
   - `src/TecmoSBGame/Audio/SoundService.cs` provides a small sound facade.
   - `content/sound/bank28_sound_engine.yaml` declares a tiny sound catalog + event map.
   - `content/sounddata/bank29_sound_data.yaml` and `content/sounddata/bank30_sound_data.yaml` model songs/SFX as symbolic note patterns.
   - `content/dmcsamples/bank32_dmc_samples.yaml` already documents the key DMC sample inventory and ROM offsets.

2. **A legacy ArchiveMge audio event bridge exists**
   - `src/TecmoSBGame/ArchiveMge/Systems/SoundSystem.cs` maps gameplay events to cues for:
     - snap
     - catch
     - interception
     - incomplete
     - hit/block contact
     - whistle
     - fumble
   - `ArchiveMge/MainGame.cs` instantiates `SoundService`, loads default cues, and updates music state.

3. **SimArch runtime has no active audio hookup**
   - `MainGameArch.cs` never creates `SoundService`.
   - `Sim.cs` never emits host-side audio playback requests.
   - Result: the current playable runtime is effectively silent even where gameplay events already exist.

4. **No real audio assets are present in the repo**
   - There are no checked-in gameplay/menu WAV/OGG/MP3 assets under project content.
   - `SoundService.LoadDefaultCues()` registers content keys such as `audio/snap`, `audio/whistle`, etc., but those assets do not exist.
   - Missing loads are silently tolerated, so the scaffold does not fail loudly.

5. **Current YAML parity is far below the original ROM sound inventory**
   - Current bank28 YAML defines only:
     - `sfx_whistle`
     - `sfx_snap`
     - `music_title`
   - Current bank29/bank30 YAML define only a few placeholder songs/SFX.
   - Original `sound_ids.asm` defines a much larger bank of gameplay, menu, crowd, DMC voice, and song IDs.

## Comparison against original sound inventory

### Original high-value effect IDs present in the disassembly

From `constants_variables/sound_ids.asm`, notable live-game and UX-relevant IDs include:
- `DOWN_DMC`
- `HUT_DMC`
- `TOUCHDOWN_DMC`
- `THROW_SOUND`
- `SNAP_TOSS_SOUND`
- `KICK_SOUND`
- `CATCH_SOUND`
- `WHISTLE_SOUND`
- `BALL_HIT_HAND_SOUND`
- `COLLISSION_SOUND`
- `WHOOSHING_AIR_SOUND`
- `CROWD_RUN_TD_SOUND`
- `CROWD_PASS_TD_SOUND`
- `CROWD_MADE_XP_SOUND`
- `PLAY_SELECTED_SOUND`
- `PLAY_MENU_SOUND`
- `FUMBLE_SOUND`

### Original high-value usage sites already identified

- `Bank20_playcall.asm`
  - repeatedly plays `PLAY_SELECTED_SOUND`
  - repeatedly plays `PLAY_MENU_SOUND`
- `Bank19_20_on_field_gameplay_loop.asm`
  - plays `KICK_SOUND`
  - plays `TOUCHDOWN_DMC`
  - plays `WHISTLE_SOUND`
  - plays `FUMBLE_SOUND`
- `Bank21_22_play_commands_on_field_logic.asm`
  - uses `SNAP_TOSS_SOUND`
  - uses `THROW_SOUND`
  - uses `CATCH_SOUND`
  - uses `DOWN_DMC`
  - uses `HUT_DMC`

### Current parity gaps vs those IDs

#### Already conceptually represented, but incomplete
- whistle
- snap
- catch
- interception/incomplete/fumble/hit as modern runtime cues
- title music

#### Missing from current runtime cue model entirely or nearly entirely
- throw/pass-release cue
- kick/boot cue
- touchdown celebration cue
- down voice cue
- hut voice cue
- playcall move/select cues mapped to actual SimArch/menu inputs
- distinct crowd reaction cues by event type
- air/whoosh/ball-flight cue
- ball-hit-hand / sharper catch deflection cue
- field-goal / XP made-miss cues
- chain / first-down / quarter/halftime/game-end stingers

## Content/asset audit

### Audio asset pipeline gaps

1. **No source audio files checked in**
   - no `assets/audio/` tree from `docs/SOUND_MUSIC_PLAN.md`
   - no generated MonoGame audio content entries

2. **No verified content-pipeline integration for audio**
   - no evidence of a `.mgcb`/content build path for gameplay sound assets in active use
   - current code assumes content keys will exist, but the project has not wired them into build outputs

3. **DMC sample mapping is documented but not executable**
   - `content/dmcsamples/bank32_dmc_samples.yaml` is useful reference data
   - but there is no extraction tool, no generated WAVs, and no runtime loader/use path for those samples

### Non-audio content parity notes surfaced during this track

The original master build shows substantial presentation/content surface area beyond current runtime coverage, including:
- play image tiles / play select screen text
- crowd/endzone/stadium tiles
- scoreboard/cutscene/celebration graphics
- player face tiles / helmet logos / scene graphics

This audit does **not** expand that into a full art backlog, but it does confirm that audio parity should be planned alongside broader presentation-bank validation rather than as isolated sound-only work.

## Highest-value backlog

Ordered for impact on the current playable slice, not for final completeness.

### 1) Wire SimArch gameplay audio events into the active runtime

**Why first**
- The current playable game host is `MainGameArch`, and it is silent.
- Existing gameplay state already exposes enough events/results to drive core cues.
- This gives immediate player feedback without requiring perfect asset parity first.

**Concrete tasks**
- Add a host-side audio bridge for `MainGameArch` + `Sim`.
- Trigger at minimum:
  - snap
  - whistle
  - catch
  - incomplete
  - interception
  - fumble
  - tackle/hit
  - menu move/select in playcall UI
- Decide whether the bridge should use:
  - explicit per-tick cue flags in `SimSnapshot`, or
  - a drained host-facing event queue exported by `Sim`
- Acceptance:
  - manual run of `MainGameArch` produces audible feedback for core play lifecycle and playcall navigation.

### 2) Create a real audio content pipeline baseline

**Why second**
- Current code paths point to non-existent assets.
- Even a perfect event bridge produces silence until content exists.

**Concrete tasks**
- Add a minimal checked-in source asset tree for placeholder-or-extracted assets:
  - `Audio/snap`
  - `Audio/whistle`
  - `Audio/catch`
  - `Audio/incomplete`
  - `Audio/interception`
  - `Audio/hit`
  - `Audio/fumble`
  - `Audio/menu_move`
  - `Audio/menu_select`
- Wire these assets into the active MonoGame content build.
- Make missing-asset failures visible in development logs instead of silently disappearing forever.
- Acceptance:
  - sound assets build with the project and load successfully in the active game host.

### 3) Expand cue coverage to original high-signal football events

**Why third**
- These are the most noticeable parity gaps after basic feedback exists.

**Concrete tasks**
- Add cues and event hooks for:
  - throw/pass release (`THROW_SOUND` parity)
  - kick/punt/FG contact (`KICK_SOUND` parity)
  - touchdown celebration (`TOUCHDOWN_DMC` / crowd parity)
  - down/dead-ball voice (`DOWN_DMC` parity)
  - hut/pre-snap vocal (`HUT_DMC` parity, if desired in scope)
  - crowd reaction split for rushing TD / passing TD / made XP
- Acceptance:
  - scoring and special-event feedback is no longer generic whistle/hit-only audio.

### 4) Replace placeholder YAML audio banks with an explicit parity map

**Why fourth**
- Current YAML is too sparse to serve as a tracking source of truth.
- The project needs a canonical mapping between ROM IDs and MonoGame cue IDs.

**Concrete tasks**
- Expand `content/sound/bank28_sound_engine.yaml` to include a fuller cue catalog.
- Add a parity table mapping original IDs to remake cue IDs/status:
  - implemented
  - placeholder
  - missing
  - intentionally deferred
- Cover at least all high-value IDs from `sound_ids.asm`.
- Acceptance:
  - a developer can inspect one YAML/doc source and see parity status without grepping assembly.

### 5) Implement music state playback for title/menu/on-field/score transitions

**Why fifth**
- `MusicState` already exists but is only a state machine stub.
- Music matters, but gameplay SFX feedback is higher priority.

**Concrete tasks**
- Implement actual `Song`/`MediaPlayer` playback in `SoundService.SetMusicState()`.
- Provide at minimum:
  - title
  - menu
  - on-field ambient/music
  - score/touchdown sting or score-state transition
- Decide whether gameplay should use music or crowd-only ambience by default.
- Acceptance:
  - title/menu/gameplay transitions audibly change music state in the active host.

### 6) Build DMC extraction/import tooling for voice parity

**Why sixth**
- The repo already contains the metadata, and the DMC voice clips are distinctive parity wins.
- But extraction/tooling is a bigger slice than basic event hookup.

**Concrete tasks**
- Add a tool/script to extract or convert the documented DMC samples from the original ROM.
- Generate usable WAV assets for:
  - `down_voice`
  - `hut_voice`
  - `touchdown_voice`
  - optionally drum/percussion samples
- Document any intentional bug fixes or authenticity choices around the original sample-offset quirks.
- Acceptance:
  - DMC-backed voice assets are reproducible from source data and hooked into the content pipeline.

### 7) Extend parity audit into presentation/content banks adjacent to audio

**Why seventh**
- Audio and presentation are tightly coupled in the original game.
- Once sound hooks exist, the next user-visible gap is often missing celebratory/menu visuals.

**Concrete tasks**
- Audit which original presentation-bank assets are already represented in current content vs missing:
  - playcall imagery
  - field/crowd/scoreboard tiles
  - touchdown/halftime/game-over presentation assets
  - faces/helmet/logo assets for menus and meta screens
- Produce a separate asset parity sheet rather than overloading this audio document.
- Acceptance:
  - remaining non-audio presentation gaps are explicit and prioritizable.

## Recommended immediate execution slices

### Slice A — "make the current game no longer silent"
- Wire SimArch host-side sound triggering
- Add minimal placeholder assets for snap/whistle/hit/catch/menu
- Validate manually in `MainGameArch`

### Slice B — "playcall and scoring feedback parity"
- Add menu move/select cues in the active playcall UI
- Add touchdown / throw / kick hooks
- Add basic crowd reaction split

### Slice C — "authoritative parity map"
- Expand bank28/bank29/bank30 YAML + add a ROM-ID mapping table doc
- Mark each cue as implemented/placeholder/missing

## Tiny safe code slice status

No code change was made in this audit pass.

Reason:
- the highest-value work is architectural and content-backed rather than a one-line isolated fix;
- a tiny non-overlapping code tweak here would risk creating another silent scaffold without assets or a full SimArch hookup path.

## Key findings for follow-on agents

- **MainGameArch is the critical missing hookup point** for audible feedback.
- **SoundService currently only works in ArchiveMge**, which is no longer the primary runtime.
- **The repo has no real audio assets yet**, so runtime hookup alone will not complete the job.
- **The original ROM already gives a strong priority list** via `sound_ids.asm` and usage in Banks 19/20/21/22/20-playcall.
- The best next task is a bounded implementation pass: **SimArch audio bridge + minimal asset pipeline baseline**.
