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
- **UI pixel font** (numbers + uppercase + punctuation)
  - Glyph grid: 8×8 → 16×16
  - Atlas: 128×64 → 256×128 (example; depends on glyph count)
  - Use: scoreboard, menus, small HUD labels.

- **Large headline font** (title/team names/menus)
  - Glyph grid: 16×16 → 32×32
  - Atlas: 256×128 → 512×256 (example)

### 0.2 Common UI pieces
- Window/panel frame tiles
  - Tile size: 8×8 → 16×16
  - Atlas: 128×128 → 256×256 (frame corners/edges/fills)

- Cursor/selector
  - Arrow cursor: ~8×8 → 16×16
  - Highlight bar ends/caps: 8×8 tiles → 16×16

- Buttons/icons
  - A/B/Start indicators: ~16×16 → 32×32
  - Controller glyphs (optional): 16×16 → 32×32

---

## 1) Title / front-end screens

### 1.1 Title screen
- Title background illustration
  - Full screen: 256×224 → 512×448
- Title logo (if separated)
  - ~200×80 → ~400×160
- “Press Start” prompt
  - ~80×16 → ~160×32

### 1.2 Main menu
- Menu background (static)
  - 256×224 → 512×448
- Menu list frame + separators
  - tile-based (see frames above)

### 1.3 Team select
- Team select background
  - 256×224 → 512×448
- Team helmet icons (per team)
  - ~24×24 → ~48×48 (or 32×32 → 64×64 if we want more detail)
  - Count: 28 teams (NES) + optional extra
- Team abbreviation/name labels
  - text render via font OR pre-rendered sprites

### 1.4 Playbook / play call UI
- Play call screen background
  - 256×224 → 512×448
- Play diagrams (routes/assignments)
  - vector-like overlays OR sprite sheet
  - If sprites: route arrows/lines in 8×8 tiles → 16×16 tiles
- Play names list panel + selector

### 1.5 Cutscenes / transitions (minimal set)
- Coin toss screen
  - 256×224 → 512×448
- Halftime summary background
  - 256×224 → 512×448
- End of game screen
  - 256×224 → 512×448

---

## 2) On-field presentation

### 2.1 Field art
Two viable approaches (we can start simple and upgrade later):

A) **Tile/atlas-driven field** (recommended)
- Field tile set (grass textures, yard lines, numbers, hashmarks, endzone patterns)
  - Tile size: 8×8 → 16×16
  - Atlas: ~256×256 → ~512×512 (estimate)
- Endzone lettering patterns
  - team-neutral set first

B) **Single background** (temporary)
- Full field background: 256×224 → 512×448

### 2.2 HUD / scoreboard overlays (in-game)
- Scoreboard strip / panels
  - Width: 256, Height: ~24 → 512×48
- Down & distance panel
  - ~80×16 → ~160×32
- Time/quarter indicator
  - digits via font + small icons
- Possession indicator (arrow/ball icon)
  - ~8×8 → 16×16

### 2.3 Camera/scroll indicators (optional)
- “Off-screen player” markers
  - ~8×8 → 16×16

---

## 3) Player sprites (on-field)

### 3.1 Player base sprites
Player sprite sizes in NES Tecmo are small; we’ll keep the *feel* but give more detail.

- Player body (main)
  - ~16×16 → ~32×32
- Player shadow/underlay (optional)
  - ~16×8 → ~32×16

### 3.2 Facing/animation sets (per uniform set)
We should plan for these animation groups (exact frame counts TBD):

- Idle/ready stance (pre-snap)
- Run cycle (with ball / without ball)
- Cut/juke (left/right)
- Dive / tackle attempt
- Grapple / engaged (block/tackle)
- Hit reaction / fall
- Celebrations (TD, big play)

> If we do team uniforms via palette swaps, we can keep a single sprite set + palette sets.

### 3.3 Uniform variants
- Home/away palette sets per team
  - Palette entries: ~4–8 ramps/team (implementation-specific)
  - Deliverable: YAML palette definitions + reference swatches (no ROM colors)

### 3.4 Ball
- Ball sprite
  - ~8×8 → 16×16
- Ball “in air” variants (optional spin frames)
  - 2–4 frames, 16×16 each

---

## 4) Special teams + officials

### 4.1 Kicking animations
- Kicker windup + kick
  - 32×32 frames
- Punter windup + punt
  - 32×32 frames

### 4.2 Referee sprites (optional but nice)
- Ref idle
  - 24×24 → 48×48 (or keep 32×32)
- Signal animations (TD, safety, first down)

---

## 5) UI feedback / effects

### 5.1 On-field effects
- Tackle impact spark/dust
  - ~16×16 → 32×32
- Whistle indicator (optional)
  - ~16×16 → 32×32
- First down line marker (optional; could be procedural)
  - If sprite: tile strip 8×8 → 16×16

### 5.2 Text callouts
- “TOUCHDOWN”, “SAFETY”, “FIRST DOWN”, “INTERCEPTION”, “FUMBLE”, etc.
  - As text via headline font OR pre-rendered banners
  - Banner size (typical): ~200×24 → ~400×48

---

## 6) Audio (clean-room)

### 6.1 SFX (one-shots)
- UI: confirm, cancel, move cursor
- Snap
- Kick (kickoff/punt)
- Punt catch (optional distinct)
- Pass throw
- Catch
- Interception sting (short)
- Fumble / loose ball
- Hit/tackle impacts (light/medium/heavy variants)
- Whistle
- Crowd swell (short loop or layered one-shots)
- Crowd boo / cheer (optional)
- Ref call beep/sting (optional)

### 6.2 Music
- Title theme (loop)
- Menu theme (loop)
- Team select theme (optional)
- On-field theme (loop; subtle)
- Touchdown stinger
- Turnover stinger
- Halftime theme (loop)
- Endgame win/lose stingers

---

## 7) Season / stat / management UI (if we ship season mode)

These screens exist in Tecmo and/or are likely for our full game loop.

- Season hub background
  - 256×224 → 512×448
- Schedule screen layout
  - 256×224 → 512×448
- Standings screen layout
  - 256×224 → 512×448
- Team roster list screen
  - 256×224 → 512×448
- Player stats screen (passing/rushing/receiving/defense)
  - 256×224 → 512×448
- Playoff bracket screen
  - 256×224 → 512×448
- Save/Load UI panels/icons
  - icon set ~16×16 → 32×32

---

## 8) Team-specific presentation (optional but very Tecmo)

- Team logos (menu scale)
  - ~64×32 → ~128×64 (per team)
- Team endzone wordmarks (field scale)
  - tile-based lettering OR a banner sprite
  - estimate: ~256×32 → ~512×64 (per team) if banner-based
- Team color/palette definitions (data, not pixels)

---

## 9) Data-driven manifests (placeholders to implement)

When we implement sprite/texture registry (Stage 2), we’ll want YAML manifests like:

- `Content/Data/Sprites/spritesheets.yml`
  - defines sheets + pixel scale + default sampler
- `Content/Data/Sprites/regions.yml`
  - defines named sprite regions (x,y,w,h)
- `Content/Data/Audio/sfx.yml`, `music.yml`

---

## 10) Tracking / acceptance criteria

For each asset above, track:
- `id`
- `file path` (under `Content/`)
- `proposed size` (px)
- `notes` (animation frames, palette swap rules, etc.)
- `status` (todo/in progress/done)

