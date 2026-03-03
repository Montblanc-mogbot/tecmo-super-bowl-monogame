# ASSETS.md (Clean-room asset list)

This file is a **planning checklist** of all original assets we need to create for the MonoGame remake.

- **Reference:** Tecmo Super Bowl (NES) is the behavioral + layout reference.
- **Clean-room:** No ROM extraction or converted ROM assets.
- **Resolution policy (proposal):** keep **NES logical layout** (256×224, 8:7), but author art at **2×** where practical.
  - **Virtual/gameplay coordinate space:** 256×224 (still used for logic/camera).
  - **Art authoring scale:** 512×448 (2×) pixel-art, rendered with point sampling.
  - UI can optionally go 3× for readability, but should respect the 8:7 framing.

> Sizes below are given as `NES_ref → proposed`.

---

## 0) Global / shared

### 0.1 Fonts
| ID | Asset | Path | Size | Notes | Status |
|---|---|---|---|---|---|
| FONT_UI_PIXEL | UI pixel font atlas | Content/Fonts/ui_pixel.png | 256×128 | Glyphs: 0-9, A-Z, punctuation, 16×16 per glyph | todo |
| FONT_HEADLINE | Large headline font | Content/Fonts/headline.png | 512×256 | Glyphs: A-Z, numbers, 32×32 per glyph | todo |
| FONT_SMALL | Small text font | Content/Fonts/small.png | 128×64 | Glyphs: numbers, uppercase, 8×8 → 16×16 | todo |

### 0.2 Common UI pieces
| ID | Asset | Path | Size | Notes | Status |
|---|---|---|---|---|---|
| UI_FRAME_TILES | Window frame tileset | Content/UI/frame_tiles.png | 256×256 | Corners, edges, fills, 16×16 tiles | todo |
| UI_CURSOR | Selection cursor | Content/UI/cursor.png | 16×16 | Arrow/selector sprite | todo |
| UI_HIGHLIGHT | Highlight bar | Content/UI/highlight.png | 32×16 | Selection highlight ends + center | todo |
| UI_BUTTONS | Button icons | Content/UI/buttons.png | 64×32 | A/B/Start button glyphs, 32×32 each | todo |
| UI_CONTROLLER | Controller glyphs | Content/UI/controller.png | 64×32 | D-pad, button icons (optional) | todo |

---

## 1) Title / front-end screens

### 1.1 Title screen
| ID | Asset | Path | Size | Notes | Status |
|---|---|---|---|---|---|
| TITLE_BG | Title background | Content/Title/bg.png | 512×448 | Full-screen illustration | todo |
| TITLE_LOGO | Title logo | Content/Title/logo.png | 400×160 | Tecmo Super Bowl logo | todo |
| TITLE_PROMPT | "Press Start" prompt | Content/Title/press_start.png | 160×32 | Blinking prompt text | todo |

### 1.2 Main menu
| ID | Asset | Path | Size | Notes | Status |
|---|---|---|---|---|---|
| MENU_BG | Menu background | Content/Menu/bg.png | 512×448 | Static background | todo |
| MENU_FRAME | Menu frame | Content/Menu/frame.png | 256×256 | Tile-based menu frame | todo |

### 1.3 Team select
| ID | Asset | Path | Size | Notes | Status |
|---|---|---|---|---|---|
| TEAMSEL_BG | Team select background | Content/TeamSelect/bg.png | 512×448 | Background art | todo |
| HELMETS | Team helmet icons | Content/TeamSelect/helmets.png | 512×256 | 28+ teams, 64×64 each, sprite sheet | todo |
| TEAM_LABELS | Team name labels | Content/TeamSelect/labels.png | 256×128 | Pre-rendered team abbreviations | todo |

### 1.4 Playbook / play call UI
| ID | Asset | Path | Size | Notes | Status |
|---|---|---|---|---|---|
| PLAYCALL_BG | Play call background | Content/PlayCall/bg.png | 512×448 | Play selection screen | todo |
| PLAY_DIAGRAMS | Play diagram overlays | Content/PlayCall/diagrams.png | 512×512 | Route arrows, lines, X/O markers | todo |
| PLAYCALL_FRAME | Play list panel | Content/PlayCall/frame.png | 128×256 | Play names list container | todo |

### 1.5 Cutscenes / transitions
| ID | Asset | Path | Size | Notes | Status |
|---|---|---|---|---|---|
| COIN_TOSS | Coin toss screen | Content/Cutscenes/coin_toss.png | 512×448 | Coin flip presentation | todo |
| HALFTIME_BG | Halftime summary | Content/Cutscenes/halftime.png | 512×448 | Halftime stats screen | todo |
| ENDGAME_BG | End of game screen | Content/Cutscenes/endgame.png | 512×448 | Final score presentation | todo |

---

## 2) On-field presentation

### 2.1 Field art (Tile/atlas-driven - recommended)
| ID | Asset | Path | Size | Notes | Status |
|---|---|---|---|---|---|
| FIELD_TILESET | Field tile atlas | Content/Field/tiles.png | 512×512 | Grass, yard lines, numbers, hashmarks | todo |
| FIELD_ENDZONES | Endzone patterns | Content/Field/endzones.png | 256×128 | Team-neutral endzone art | todo |
| FIELD_NUMBERS | Yard line numbers | Content/Field/numbers.png | 256×64 | 0-50 yard markers | todo |

### 2.2 HUD / scoreboard overlays
| ID | Asset | Path | Size | Notes | Status |
|---|---|---|---|---|---|
| HUD_SCOREBOARD | Scoreboard strip | Content/HUD/scoreboard.png | 512×48 | Score/quarter/time panel | todo |
| HUD_DOWNDIST | Down & distance | Content/HUD/down_distance.png | 160×32 | Current down + yards to go | todo |
| HUD_POSSESSION | Possession arrow | Content/HUD/possession.png | 16×16 | Ball possession indicator | todo |
| HUD_OFFSCREEN | Off-screen markers | Content/HUD/offscreen.png | 16×16 | Player position indicators | todo |

---

## 3) Player sprites (on-field)

### 3.1 Player base sprites
| ID | Asset | Path | Size | Notes | Status |
|---|---|---|---|---|---|
| PLAYER_BODY | Player body base | Content/Sprites/player_base.png | 256×256 | 32×32 frames, idle/run/cut/dive/tackle | todo |
| PLAYER_SHADOW | Player shadow | Content/Sprites/shadow.png | 32×16 | Shadow underlay sprite | todo |

### 3.2 Animation sets (per uniform set)
| ID | Asset | Path | Size | Notes | Status |
|---|---|---|---|---|---|
| ANIM_IDLE | Idle stance | Content/Sprites/anim_idle.png | 64×32 | 2-frame idle, pre-snap | todo |
| ANIM_RUN | Run cycle | Content/Sprites/anim_run.png | 256×32 | 8-frame run with/without ball | todo |
| ANIM_CUT | Cut/juke | Content/Sprites/anim_cut.png | 128×32 | 4-frame left/right juke | todo |
| ANIM_DIVE | Dive/tackle attempt | Content/Sprites/anim_dive.png | 128×32 | 4-frame dive animation | todo |
| ANIM_GRAPPLE | Grapple/engaged | Content/Sprites/anim_grapple.png | 64×32 | 2-frame block/tackle engaged | todo |
| ANIM_HIT | Hit reaction | Content/Sprites/anim_hit.png | 128×32 | 4-frame fall reaction | todo |
| ANIM_CELEBRATION | Touchdown celebration | Content/Sprites/anim_td.png | 128×32 | 4-frame celebration | todo |

### 3.3 Uniform variants
| ID | Asset | Path | Size | Notes | Status |
|---|---|---|---|---|---|
| PALETTES_HOME | Home uniform palettes | Content/Data/palettes_home.yml | - | Color ramps per team (data) | todo |
| PALETTES_AWAY | Away uniform palettes | Content/Data/palettes_away.yml | - | Color ramps per team (data) | todo |

### 3.4 Ball
| ID | Asset | Path | Size | Notes | Status |
|---|---|---|---|---|---|
| SPRITE_BALL | Ball sprite | Content/Sprites/ball.png | 16×16 | Standard ball | todo |
| ANIM_BALL_SPIN | Ball spin frames | Content/Sprites/ball_spin.png | 64×16 | 4-frame in-air rotation | todo |

---

## 4) Special teams + officials

### 4.1 Kicking animations
| ID | Asset | Path | Size | Notes | Status |
|---|---|---|---|---|---|
| ANIM_KICK | Kicker windup/kick | Content/Sprites/anim_kick.png | 128×32 | 4-frame kick sequence | todo |
| ANIM_PUNT | Punter windup/punt | Content/Sprites/anim_punt.png | 128×32 | 4-frame punt sequence | todo |

### 4.2 Referee sprites
| ID | Asset | Path | Size | Notes | Status |
|---|---|---|---|---|---|
| REFEREE_IDLE | Ref idle stance | Content/Sprites/referee.png | 48×48 | Neutral standing pose | todo |
| ANIM_REF_SIGNAL | Referee signals | Content/Sprites/anim_ref.png | 192×48 | 4-frame TD/safety/first down | todo |

---

## 5) UI feedback / effects

### 5.1 On-field effects
| ID | Asset | Path | Size | Notes | Status |
|---|---|---|---|---|---|
| FX_IMPACT | Tackle impact | Content/Effects/impact.png | 32×32 | Spark/dust effect | todo |
| FX_WHISTLE | Whistle indicator | Content/Effects/whistle.png | 32×32 | Play over icon | todo |
| FX_FIRSTDOWN | First down line | Content/Effects/firstdown.png | 16×16 | Yellow line marker tile | todo |

### 5.2 Text callouts
| ID | Asset | Path | Size | Notes | Status |
|---|---|---|---|---|---|
| CALLOUT_TD | "TOUCHDOWN" banner | Content/Effects/callout_td.png | 400×48 | Big play announcement | todo |
| CALLOUT_SAFETY | "SAFETY" banner | Content/Effects/callout_safety.png | 400×48 | Safety announcement | todo |
| CALLOUT_FIRSTDOWN | "FIRST DOWN" banner | Content/Effects/callout_fd.png | 400×48 | First down announcement | todo |
| CALLOUT_INT | "INTERCEPTION" banner | Content/Effects/callout_int.png | 400×48 | Turnover announcement | todo |
| CALLOUT_FUMBLE | "FUMBLE" banner | Content/Effects/callout_fumble.png | 400×48 | Fumble announcement | todo |

---

## 6) Audio (clean-room)

### 6.1 SFX (one-shots)
| ID | Asset | Path | Format | Notes | Status |
|---|---|---|---|---|---|
| SFX_UI_CONFIRM | UI confirm | Content/Audio/SFX/ui_confirm.wav | WAV | Menu selection | todo |
| SFX_UI_CANCEL | UI cancel | Content/Audio/SFX/ui_cancel.wav | WAV | Menu back | todo |
| SFX_UI_MOVE | UI cursor | Content/Audio/SFX/ui_move.wav | WAV | Menu navigation | todo |
| SFX_SNAP | Ball snap | Content/Audio/SFX/snap.wav | WAV | Center snap | todo |
| SFX_KICK | Kick | Content/Audio/SFX/kick.wav | WAV | Kickoff/punt | todo |
| SFX_CATCH | Catch | Content/Audio/SFX/catch.wav | WAV | Pass reception | todo |
| SFX_THROW | Pass throw | Content/Audio/SFX/throw.wav | WAV | QB release | todo |
| SFX_INT_STING | Interception | Content/Audio/SFX/interception.wav | WAV | INT alert | todo |
| SFX_FUMBLE | Fumble | Content/Audio/SFX/fumble.wav | WAV | Loose ball | todo |
| SFX_HIT_LIGHT | Light hit | Content/Audio/SFX/hit_light.wav | WAV | Small tackle | todo |
| SFX_HIT_MEDIUM | Medium hit | Content/Audio/SFX/hit_medium.wav | WAV | Standard tackle | todo |
| SFX_HIT_HEAVY | Heavy hit | Content/Audio/SFX/hit_heavy.wav | WAV | Big collision | todo |
| SFX_WHISTLE | Whistle | Content/Audio/SFX/whistle.wav | WAV | Play over | todo |
| SFX_CROWD_SWELL | Crowd swell | Content/Audio/SFX/crowd_swell.wav | WAV | Excitement burst | todo |
| SFX_CROWD_CHEER | Crowd cheer | Content/Audio/SFX/cheer.wav | WAV | Positive play | todo |
| SFX_CROWD_BOO | Crowd boo | Content/Audio/SFX/boo.wav | WAV | Negative play | todo |
| SFX_REF_BEEP | Ref signal | Content/Audio/SFX/ref_beep.wav | WAV | Official whistle | todo |

### 6.2 Music
| ID | Asset | Path | Format | Notes | Status |
|---|---|---|---|---|---|
| MUSIC_TITLE | Title theme | Content/Audio/Music/title.ogg | OGG | Looping title music | todo |
| MUSIC_MENU | Menu theme | Content/Audio/Music/menu.ogg | OGG | Looping menu music | todo |
| MUSIC_TEAMSEL | Team select | Content/Audio/Music/teamsel.ogg | OGG | Looping selection music | todo |
| MUSIC_INGAME | On-field theme | Content/Audio/Music/ingame.ogg | OGG | Subtle gameplay music | todo |
| MUSIC_HALFTIME | Halftime theme | Content/Audio/Music/halftime.ogg | OGG | Looping halftime music | todo |
| STING_TD | Touchdown stinger | Content/Audio/Music/sting_td.ogg | OGG | Short TD celebration | todo |
| STING_TURNOVER | Turnover stinger | Content/Audio/Music/sting_turnover.ogg | OGG | Short turnover alert | todo |
| STING_WIN | Victory stinger | Content/Audio/Music/sting_win.ogg | OGG | Game win celebration | todo |
| STING_LOSE | Defeat stinger | Content/Audio/Music/sting_lose.ogg | OGG | Game loss theme | todo |

---

## 7) Season / stat / management UI

| ID | Asset | Path | Size | Notes | Status |
|---|---|---|---|---|---|
| SEASON_HUB_BG | Season hub | Content/Season/hub_bg.png | 512×448 | Season mode main | todo |
| SEASON_SCHEDULE_BG | Schedule screen | Content/Season/schedule_bg.png | 512×448 | Weekly schedule | todo |
| SEASON_STANDINGS_BG | Standings screen | Content/Season/standings_bg.png | 512×448 | Division standings | todo |
| SEASON_ROSTER_BG | Roster screen | Content/Season/roster_bg.png | 512×448 | Team roster list | todo |
| SEASON_STATS_BG | Player stats | Content/Season/stats_bg.png | 512×448 | Passing/rushing/defense | todo |
| SEASON_PLAYOFFS_BG | Playoff bracket | Content/Season/playoffs_bg.png | 512×448 | Postseason bracket | todo |
| UI_SAVELOAD | Save/Load icons | Content/UI/saveload.png | 64×64 | 4 icons, 32×32 each | todo |

---

## 8) Team-specific presentation

| ID | Asset | Path | Size | Notes | Status |
|---|---|---|---|---|---|
| TEAM_LOGOS | Team logos | Content/Team/logos.png | 1024×256 | 28+ teams, 128×64 each | todo |
| TEAM_ENDZONES | Endzone wordmarks | Content/Team/endzones.png | 2048×64 | 28+ teams, 512×64 each | todo |
| TEAM_COLORS | Color definitions | Content/Data/team_colors.yml | - | RGB values per team | todo |

---

## 9) Data-driven manifests

| ID | Asset | Path | Format | Notes | Status |
|---|---|---|---|---|---|
| MANIFEST_SPRITES | Sprite registry | Content/Data/sprites.yml | YAML | Sheets + regions | todo |
| MANIFEST_AUDIO_SFX | SFX manifest | Content/Data/sfx.yml | YAML | Sound effect mapping | todo |
| MANIFEST_AUDIO_MUSIC | Music manifest | Content/Data/music.yml | YAML | Music track mapping | todo |
| MANIFEST_ANIMATIONS | Animation clips | Content/Data/animations.yml | YAML | Frame timing data | todo |

---

## 10) Asset Summary

| Category | Count | Priority |
|---|---|---|
| Fonts | 3 | High |
| UI Pieces | 5 | High |
| Title/Menu Screens | 8 | High |
| Team Select | 3 | High |
| Play Call UI | 3 | Medium |
| Cutscenes | 3 | Medium |
| Field Art | 3 | High |
| HUD Overlays | 4 | High |
| Player Sprites | 10 | High |
| Player Animations | 8 | High |
| Uniform Palettes | 2 | High |
| Ball Sprites | 2 | High |
| Special Teams/Ref | 3 | Medium |
| Effects | 3 | Medium |
| Text Callouts | 5 | Low |
| SFX | 17 | High |
| Music | 9 | Medium |
| Season UI | 7 | Low |
| Team Presentation | 3 | Low |
| Data Manifests | 4 | High |
| **TOTAL** | **104** | - |

---

## Asset Directory Structure

```
Content/
├── Fonts/
│   ├── ui_pixel.png
│   ├── headline.png
│   └── small.png
├── UI/
│   ├── frame_tiles.png
│   ├── cursor.png
│   ├── highlight.png
│   ├── buttons.png
│   ├── controller.png
│   └── saveload.png
├── Title/
│   ├── bg.png
│   ├── logo.png
│   └── press_start.png
├── Menu/
│   ├── bg.png
│   └── frame.png
├── TeamSelect/
│   ├── bg.png
│   ├── helmets.png
│   └── labels.png
├── PlayCall/
│   ├── bg.png
│   ├── diagrams.png
│   └── frame.png
├── Cutscenes/
│   ├── coin_toss.png
│   ├── halftime.png
│   └── endgame.png
├── Field/
│   ├── tiles.png
│   ├── endzones.png
│   └── numbers.png
├── HUD/
│   ├── scoreboard.png
│   ├── down_distance.png
│   ├── possession.png
│   └── offscreen.png
├── Sprites/
│   ├── player_base.png
│   ├── shadow.png
│   ├── ball.png
│   ├── ball_spin.png
│   ├── referee.png
│   ├── anim_idle.png
│   ├── anim_run.png
│   ├── anim_cut.png
│   ├── anim_dive.png
│   ├── anim_grapple.png
│   ├── anim_hit.png
│   ├── anim_td.png
│   ├── anim_kick.png
│   ├── anim_punt.png
│   └── anim_ref.png
├── Effects/
│   ├── impact.png
│   ├── whistle.png
│   ├── firstdown.png
│   ├── callout_td.png
│   ├── callout_safety.png
│   ├── callout_fd.png
│   ├── callout_int.png
│   └── callout_fumble.png
├── Team/
│   ├── logos.png
│   └── endzones.png
├── Season/
│   ├── hub_bg.png
│   ├── schedule_bg.png
│   ├── standings_bg.png
│   ├── roster_bg.png
│   ├── stats_bg.png
│   └── playoffs_bg.png
├── Audio/
│   ├── SFX/
│   │   ├── ui_confirm.wav
│   │   ├── ui_cancel.wav
│   │   ├── ui_move.wav
│   │   ├── snap.wav
│   │   ├── kick.wav
│   │   ├── catch.wav
│   │   ├── throw.wav
│   │   ├── interception.wav
│   │   ├── fumble.wav
│   │   ├── hit_light.wav
│   │   ├── hit_medium.wav
│   │   ├── hit_heavy.wav
│   │   ├── whistle.wav
│   │   ├── crowd_swell.wav
│   │   ├── cheer.wav
│   │   ├── boo.wav
│   │   └── ref_beep.wav
│   └── Music/
│       ├── title.ogg
│       ├── menu.ogg
│       ├── teamsel.ogg
│       ├── ingame.ogg
│       ├── halftime.ogg
│       ├── sting_td.ogg
│       ├── sting_turnover.ogg
│       ├── sting_win.ogg
│       └── sting_lose.ogg
└── Data/
    ├── palettes_home.yml
    ├── palettes_away.yml
    ├── team_colors.yml
    ├── sprites.yml
    ├── sfx.yml
    ├── music.yml
    └── animations.yml
```
