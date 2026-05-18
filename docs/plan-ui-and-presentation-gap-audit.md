# UI and presentation gap audit

Updated: 2026-05-07

## Scope

Audit the current launch-to-game UX for the Arch runtime and compare it against:
- `docs/FULL_COMPLETION_PLAN.md` phase 4 expectations
- existing front-end renderers, SimArch flow state, and menu/playcall content
- original-content-oriented banks already represented in `content/`

This document focuses on the bounded path to a coherent **exhibition** UX:
- title
- main menu
- team select
- coin toss / kickoff bridge
- playcall
- in-game HUD
- post-play
- pause

## Executive summary

The current Arch runtime is still a **field-first debug slice**, not a front-end flow.

Good news: the repo already contains most of the *pieces* needed for a coherent exhibition presentation pass:
- dedicated renderers for title, main menu, team select, coin toss, scoreboard, and post-play summary
- a SimArch `GameFlowController` with the right high-level states (`Title`, `MainMenu`, `TeamSelect`, `CoinToss`, `Kickoff`, `OnField`, `PostPlay`)
- team text/content banks and basic menu/playcall YAML scaffolding
- a usable minimal playcall overlay already wired in Arch

But those pieces are mostly **not connected in `MainGameArch`**. The actual Arch launch path still goes straight into on-field rendering with debug-style overlays and no launch/menu/team-select/pause surfaces. That makes the current runtime functional for development, but not coherent for a player.

## Current state by surface

### 1. Title screen

**What exists**
- `src/TecmoSBGame/Rendering/TitleScreenRenderer.cs`
- A proper screen-sized renderer with blinking `PRESS START`
- `GameFlowController` supports `GameFlowState.Title`

**What is missing in Arch runtime**
- `MainGameArch` never instantiates or draws `TitleScreenRenderer`
- no title-state ownership in `MainGameArch`
- no transition from title into menu via a flow controller
- no attract/demo behavior or title music hookup

**Assessment**
- Renderer exists, but the feature is effectively unused.

### 2. Main menu

**What exists**
- `src/TecmoSBGame/Rendering/MainMenuRenderer.cs`
- `GameFlowController.SelectMainMenuItem(...)`
- menu item enums in `src/TecmoSBGame/SimArch/Components/Menu.cs`
- content scaffolding in `content/menuscripts/main_menu.yaml`

**What is missing**
- `MainGameArch` does not create or draw `MainMenuRenderer`
- no Arch menu navigation system is wired (`src/TecmoSBGame/SimArch/Systems/Menu/MenuNavigationSystem.cs` is still TODO)
- `content/menuscripts/main_menu.yaml` is only a placeholder note, not executable layout/data
- no disabled/incomplete-mode treatment for non-exhibition items (`Season`, `Pro Bowl`, `Options`, `Data`)

**Assessment**
- Visual renderer exists, but menu behavior is still effectively the old MGE path only.
- For the minimal exhibition UX, the menu can remain simple and route only `PRESEASON` forward, but it must be actually wired in Arch.

### 3. Team select

**What exists**
- `src/TecmoSBGame/Rendering/TeamSelectRenderer.cs`
- `GameFlowController` team-select state and selection logic
- team content in `content/teamtext/bank16_team_text_data.yaml`

**What is missing**
- `MainGameArch` does not instantiate or render `TeamSelectRenderer`
- `Sim`/Arch host does not currently carry a front-end `GameFlowController`
- selected teams are not flowed into a coherent exhibition boot path before on-field play
- team colors/logos/conference framing are still placeholder-grade; ratings are deterministic fake values, not real team-derived presentation data

**Assessment**
- This is close to usable for a minimal exhibition flow once wired, but still presentation-light.

### 4. Coin toss / kickoff bridge

**What exists**
- `src/TecmoSBGame/Rendering/CoinTossRenderer.cs`
- `GameFlowController` support for `CoinToss` and `Kickoff`
- `MatchState.ResetForKickoff(...)`
- kickoff systems exist in SimArch (`KickoffFlightStartSystem`, `KickoffFlightCompleteSystem`)

**What is missing**
- `MainGameArch` does not render coin toss or kickoff setup screens
- no arch host logic bridges selected teams -> coin toss -> kickoff presentation -> on-field start
- kickoff state exists architecturally, but not as a player-facing launch bridge
- no dedicated kickoff setup copy/status UI

**Assessment**
- This is the biggest “hidden progress” area: architecture exists, but the user cannot see or drive it from Arch.

### 5. Playcall UI

**What exists**
- `src/TecmoSBGame/Rendering/PlayCallOverlayRenderer.cs` is wired in `MainGameArch`
- `src/TecmoSBGame/UI/Playcall/PlaycallScreen.cs` provides a code-only Gum scaffold
- SimArch playcall state is exposed in `SimSnapshot.PlayCall`
- `src/TecmoSBGame/SimArch/Systems/PlayCall/PlayCallSystem.cs` is active

**What is missing**
- no Gum integration in the active Arch host path
- the overlay is still debug/demo-style, not final product presentation
- current playcall content (`content/playcall/bank20_playcall.yaml`) is only a simplified scaffold and does not yet reflect a full Tecmo-style offense/defense selection flow
- no explicit offense/defense ownership messaging, page labels, or richer play metadata

**Assessment**
- This is the one surface that is already minimally usable in Arch.
- It should remain the near-term path; replacing it with Gum now would slow exhibition UX completion.

### 6. In-game HUD

**What exists**
- `src/TecmoSBGame/Rendering/Hud/HudRenderer.cs` is drawn from `SimRenderer`
- `SimSnapshot.Hud` carries quarter, clock, score, down/distance, and spot
- `src/TecmoSBGame/Rendering/ScoreboardRenderer.cs` exists as a more Tecmo-like strip

**What is missing**
- active Arch rendering uses `HudRenderer`, not `ScoreboardRenderer`
- `HudRenderer` is intentionally minimal text, while `ScoreboardRenderer` is unused and currently typed against the old `TecmoSBGame.State.MatchState`, not the SimArch state type
- no possession indicator, timeout/quarter-transition messaging, or stronger team identity treatment
- no differentiation between debug info and player-facing HUD

**Assessment**
- The current HUD is serviceable for development but not yet product-coherent.
- The minimal path is to upgrade the current SimArch HUD/score strip, not attempt a broad UI architecture rewrite.

### 7. Post-play presentation

**What exists**
- `src/TecmoSBGame/Rendering/PostPlay/PostPlaySummaryRenderer.cs`
- SimArch lifecycle supports `PostPlay` phase in `PlayLifecycleSystem`

**What is missing**
- `MainGameArch` does not draw `PostPlaySummaryRenderer`
- `SimSnapshot` does not currently expose a dedicated post-play summary model
- the active Arch view relies on continuing field/debug rendering and playcall cycling rather than an explicit post-play panel
- no clear first-down / turnover / score banner in the live Arch path

**Assessment**
- Another almost-ready renderer stranded outside the active host path.
- Wiring this is high value because it immediately makes the play loop understandable.

### 8. Pause

**What exists**
- There is pause-oriented input vocabulary elsewhere in the project (`InputManager.OnPause`, `ControlState.Paused` in SimArch)
- docs mention pause as part of the target front-end flow

**What is missing**
- no pause state in `GameFlowState`
- no pause overlay renderer in active Arch runtime
- `MainGameArch` does not expose a pause command or suspend/update policy
- no resume / quit-to-title behavior

**Assessment**
- Pause is effectively absent in the Arch experience.
- For a coherent exhibition UX, a very small pause overlay is enough.

## Comparison against source-oriented content/docs

### Original/menu bank alignment

`docs/FULL_COMPLETION_PLAN.md` points to the original presentation banks:
- `Bank15_faces_playbooks.asm`
- `Bank16_menu_screens_slidebar.asm`
- `Bank24_draw_script_engine.asm`
- `Bank25_leaders_player_data_pro_bowl_abbrev.asm`

The repo already has corresponding content buckets:
- `content/menuscripts/`
- `content/faces/`
- `content/leaders/`
- `content/teamtext/`

But for front-end exhibition UX, these are still mostly **data placeholders** rather than gameplay-connected front-end screens.

Notable gaps:
- `content/menuscripts/main_menu.yaml` and `preseason_menu.yaml` are placeholder notes only
- `content/faces/faces.yaml` is effectively empty scaffold content
- no active renderer path yet uses faces/leaders/menu script content to drive real front-end screens

### Design-doc alignment

`docs/DESIGN.md` describes a more complete state machine including title, main menu, team select, play call, on-field, post-play, and season menus. The active Arch host does not currently implement that state-machine UX; it only implements the on-field loop plus playcall overlay.

## Recommended minimal path to a coherent exhibition UX

Recommendation: **do not attempt a full Gum/menu-script architecture pass first**.

Instead, finish a bounded Arch-native exhibition flow by wiring the existing renderers and adding only the smallest new presentation glue.

### Minimal target flow

1. **Title**
   - launch into title screen
   - `Enter`/Start advances to main menu
2. **Main menu**
   - simple highlighted list
   - only `PRESEASON` proceeds; other items can show `NOT READY` or remain non-interactive
3. **Team select**
   - choose away/home teams
   - `Enter` confirms and advances
4. **Coin toss**
   - resolve winner and receive/kick choice
   - short confirmation bridge into kickoff/on-field
5. **On-field pre-snap**
   - upgraded scoreboard strip + playcall overlay
6. **Post-play**
   - explicit result banner/panel, then continue back to pre-snap
7. **Pause**
   - lightweight pause overlay with resume / return-to-title

This path is enough to satisfy the “launch-to-exhibition” acceptance target without overcommitting to unfinished season/data/audio/UI architecture.

## Concrete backlog

Ordered by leverage and minimal overlap.

### A. Wire front-end flow into `MainGameArch`

**Goal:** make the existing Arch host own screen/flow transitions.

**Target files**
- `src/TecmoSBGame/MainGameArch.cs`
- `src/TecmoSBGame/SimArch/Flow/GameFlowController.cs`
- `src/TecmoSBGame/SimArch/Flow/GameFlowState.cs`

**Tasks**
- instantiate a `GameFlowController` in `MainGameArch`
- instantiate and retain renderers for title/main-menu/team-select/coin-toss/post-play
- route keyboard input by flow state instead of always pushing directly into on-field controls
- gate `_sim.SetInput(...)` / `_sim.SetUiButtons(...)` so field controls only apply during the on-field states
- explicitly transition launch → title → main menu → team select → coin toss → on-field
- decide whether `PostPlay` is owned by `Sim.PlayState.Phase` alone or mirrored into `GameFlowController`; keep one clear authority

**Why first**
- This unlocks almost every existing renderer with limited new code.

### B. Add a tiny pause state and overlay

**Target files**
- `src/TecmoSBGame/SimArch/Flow/GameFlowState.cs`
- `src/TecmoSBGame/MainGameArch.cs`
- new file: `src/TecmoSBGame/Rendering/PauseRenderer.cs`

**Tasks**
- add a `Paused` screen/state
- map a single key (likely `Escape`) to pause/resume during on-field and post-play
- render a compact overlay with `RESUME` / `QUIT TO TITLE`

**Why second**
- It is small, self-contained, and rounds out the minimum player-facing loop.

### C. Promote post-play from hidden lifecycle to visible UX

**Target files**
- `src/TecmoSBGame/MainGameArch.cs`
- `src/TecmoSBGame/Rendering/PostPlay/PostPlaySummaryRenderer.cs`
- `src/TecmoSBGame/SimArch/SimSnapshot.cs`
- possibly `src/TecmoSBGame/SimArch/Sim.cs`

**Tasks**
- render `PostPlaySummaryRenderer` whenever the SimArch play phase is `PostPlay`
- either adapt the renderer to SimArch state directly or add a small snapshot DTO for post-play summary data
- ensure the continue prompt is obvious and consistent

**Why third**
- This materially improves the play loop without touching deeper simulation.

### D. Replace the text-only in-game HUD with a coherent score strip

**Target files**
- `src/TecmoSBGame/Rendering/SimRenderer.cs`
- `src/TecmoSBGame/Rendering/Hud/HudRenderer.cs`
- `src/TecmoSBGame/Rendering/ScoreboardRenderer.cs`
- `src/TecmoSBGame/SimArch/SimSnapshot.cs`

**Tasks**
- either port `ScoreboardRenderer` to SimArch snapshot data or fold its layout into the active HUD path
- show team abbreviations instead of generic `AWAY/HOME` where possible
- preserve down/distance and ball spot in a stable presentation location
- move debug-only cues out of the primary player HUD path where feasible

**Why fourth**
- This is the most visible polish win during actual gameplay.

### E. Harden team-select presentation using existing content

**Target files**
- `src/TecmoSBGame/Rendering/TeamSelectRenderer.cs`
- `src/TecmoSBGame/GameContent.cs` and/or team data loaders if needed
- `content/teamtext/bank16_team_text_data.yaml`

**Tasks**
- replace fake ratings with either hidden ratings removal or real content-derived display
- surface conference/division/team full-name info more clearly
- if available, apply team colors consistently

**Why fifth**
- Nice value, but not required to make the flow coherent.

### F. Keep playcall overlay as the near-term solution; postpone Gum

**Target files**
- `src/TecmoSBGame/Rendering/PlayCallOverlayRenderer.cs`
- `src/TecmoSBGame/SimArch/Systems/PlayCall/PlayCallSystem.cs`
- `content/playcall/bank20_playcall.yaml`

**Tasks**
- improve copy/layout only as needed for clarity
- add offense/defense context if currently ambiguous
- defer `UI/Playcall/PlaycallScreen.cs` activation until the rest of the front-end is coherent

**Why**
- The overlay already works; replacing it now is not the shortest path.

### G. Defer menu-script/faces/leaders-driven productization

**Target files**
- `content/menuscripts/*.yaml`
- `content/faces/faces.yaml`
- `content/leaders/*.yaml`
- future menu/roster/leaders renderers

**Tasks**
- convert placeholder menu scripts into executable screen data later
- wire faces/leaders/roster screens after exhibition completeness

**Why deferred**
- These are phase-4 breadth items, but not part of the smallest coherent exhibition path.

## Suggested acceptance checklist for the minimal exhibition UX

- game launches to title, not straight to field
- player can reach main menu and start exhibition/preseason without hidden controls
- player can select away/home teams visibly
- coin toss / receive-kick choice is understandable
- player reaches playable on-field state with visible score/down-distance context
- playcall selection remains visible and understandable before snap
- after each play, a result screen/panel explains what happened
- player can pause, resume, and return to title

## Recommended implementation order

1. front-end flow wiring in `MainGameArch`
2. post-play overlay hookup
3. pause overlay
4. HUD/score strip upgrade
5. team-select cleanup
6. optional playcall overlay polish
7. broader menu-script/faces/leaders work later

## Tiny safe code slice recommendation

No code change included in this audit.

Reason: the repo currently has several in-progress working-tree edits in the same UI/Arch area (`MainGameArch`, `Sim`, `SimRenderer`, `SimSnapshot`, `PlayCallSystem`, movement/control files). A tiny “safe non-overlapping” slice is not obvious without risking collision with the main working set. The highest-value next step is this written backlog so the main agent can choose a bounded implementation slice deliberately.
