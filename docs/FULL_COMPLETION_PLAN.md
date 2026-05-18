# Tectonic Super Bowl — full completion plan

Updated: 2026-05-07

## Basis for this plan

This plan is derived from:
- the current SimArch repo state
- the original disassembly clone at `/home/montblanc/repos/Tecmo_Super_Bowl_NES_Disassembly`
- the original bank/module layout in `master_build.asm`
- current project docs (`DESIGN.md`, `SAVE_SEASON_DESIGN.md`, `SOUND_MUSIC_PLAN.md`, `2-plays-playable-checklist.md`)

The original source breaks the game into a few clear product areas:
- **Core game/runtime**: `Bank17_18_main_game_loop.asm`, `Bank19_20_on_field_gameplay_loop.asm`, `Bank20_playcall.asm`, `Bank21_22_play_commands_on_field_logic.asm`, `Bank23_draw_field_ball_ani_coll_check.asm`
- **Presentation/UI**: `Bank14_pal_fall_player_anim.asm`, `Bank15_faces_playbooks.asm`, `Bank16_menu_screens_slidebar.asm`, `Bank24_draw_script_engine.asm`, `Bank25_leaders_player_data_pro_bowl_abbrev.asm`
- **Meta/season/cutscenes**: `Bank7_scene_scripts.asm`, `Bank8_scene_scripts.asm`, `Bank12_13_sim_update_stats.asm`, `Bank26_misc.asm`, `Bank27_misc.asm`
- **Audio**: `Bank28_sound_engine.asm`, `Bank29_sound_data.asm`, `Bank30_sound_data.asm`, `Bank32_DMC_Samples_reset_vector.asm`
- **Data/content**: team, formation, play, scene, menu, palette, field, and sound data banks already represented in `content/`

## Current status summary

The current game is a **working vertical slice**, not a full game:
- build passes
- a human-playable pre-snap/playcall/snap loop exists
- deterministic player control exists
- a basic offensive/defensive script path exists
- basic HUD/debug rendering exists
- headless 2-play smoke coverage exists

The largest remaining work is not one bug; it is the remaining gap between a good scrimmage slice and a full Tecmo-style football game.

---

## Phase 1 — finish full scrimmage football loop

### Goal
Turn the current vertical slice into a full, repeatable football game loop for standard plays from snap through whistle across many drives.

### Remaining work
1. **Passing completeness**
   - complete QB dropback, read progression, throw timing, catch/incomplete/interception outcomes
   - remove remaining placeholder/generated route behavior where ROM/YAML data should drive behavior
   - verify pass flight start/end, receiver targeting, and defensive coverage interactions
2. **Blocking and engagement parity**
   - deepen blocker assignments beyond current nearest-lane heuristics
   - add stable block win/lose resolution and disengage behavior
   - tie double-team and rush outcomes into real play results
3. **Defensive AI completeness**
   - harden man/zone coverage behavior
   - improve pursuit, containment, and reaction to ball state changes
   - ensure defender role behavior is formation-aware and deterministic
4. **Ball state / turnovers**
   - finish fumble lifecycle, loose-ball chase, recovery, possession swaps, and reset handling
   - verify interception and turnover transitions feed scoreboard/down-distance correctly
5. **Play-end and reset reliability**
   - whistle, spot, down-distance, possession, clock, and next-play reset must hold over repeated drives
   - confirm yards gained/lost, first downs, touchdowns, safeties, touchbacks, and turnovers all update state correctly
6. **Drive-length validation**
   - extend headless coverage from 2 plays to full deterministic drive scenarios
   - add assertions for multi-play state transitions and scoring transitions

### Acceptance
A human can play repeated offensive and defensive snaps through full drives without state corruption, and headless scenarios cover the main run/pass/turnover/scoring paths.

---

## Phase 2 — special teams and all scoring paths

### Goal
Support the non-scrimmage football paths required for a real playable game.

### Remaining work
1. **Kickoff flow**
   - kickoff launch, flight, catch/recovery, return, touchback/out-of-bounds handling
   - post-score kickoff-after-score transitions
2. **Punt flow**
   - punt snap/kick, coverage lanes, return, muff/loose-ball possibilities, downing, touchback
3. **Field goal / extra point flow**
   - field-goal snap/hold/kick, block rush, success/fail outcomes, post-score continuation
4. **Scoring / special-case rules**
   - PAT, two-point decision handling if in scope
   - safety, blocked kick, onside or explicitly defer if out of scope

### Acceptance
Every standard football possession transition has a valid playable path, not just normal scrimmage snaps.

---

## Phase 3 — game-state, rules, and scoreboard completeness

### Goal
Make the simulation rules complete enough for an actual game, not just isolated plays.

### Remaining work
1. **Clock and quarter management**
   - quarter/end-of-half/end-of-game transitions
   - clock run/stop behavior by play result
2. **Possession and field position rules**
   - kickoff start states, halftime flow, side changes, touchbacks, safeties
3. **Penalty system**
   - decide scope: full penalties vs intentionally deferred
   - if included, connect rules, signals, enforcement, and replay of down/state updates
4. **Stats accumulation**
   - per-play and per-game stats for rushing/passing/receiving/defense/kicking
   - team totals and leaders
5. **Replay / post-play data capture**
   - enough replay/event capture for post-play summary and later season stats/reporting

### Acceptance
A full game can run from opening kickoff to final whistle with correct clock, score, possession, and box-score state.

---

## Phase 4 — UI, menus, and presentation from demo to product

### Goal
Replace demo-heavy surfaces with a coherent playable front end.

### Remaining work
1. **Playcall UI hardening**
   - decide temporary overlay vs Gum route for the near term
   - support stable offense/defense play selection UX, labels, pages, and feedback
2. **HUD and post-play presentation**
   - final down/distance/clock/score/possession displays
   - post-play result summary, first down/touchdown messaging, change-of-possession feedback
3. **Main menu and front-end flow**
   - title, main menu, team select, exhibition setup, pause/resume
4. **Roster/playbook/faces/leaders screens**
   - player/team data views backed by content and roster data
5. **Visual polish**
   - field rendering cleanup, camera behavior, sprite animation parity, palette correctness, reduced debug-only visuals in default mode

### Acceptance
A player can launch the game, navigate to a match, play it, and understand the state without relying on debug knowledge.

---

## Phase 5 — season, save, and meta-game features

### Goal
Implement the non-exhibition game around the on-field engine.

### Remaining work
1. **Save system**
   - settings save, season slots, quick save, record persistence
2. **Season schedule and standings**
   - week progression, played/simmed games, standings and playoff picture
3. **Season simulation**
   - CPU-vs-CPU sim, stat generation, injuries/condition if in scope
4. **Leaders / records / Pro Bowl / playoffs**
   - leaderboards, records, postseason bracket, championship flow
5. **Scene/cutscene hooks**
   - season/static scenes originally represented in Banks 7/8/25/26/27

### Acceptance
The project supports more than exhibition play: at minimum, a season can be created, progressed, saved, resumed, and summarized.

---

## Phase 6 — audio, content parity, and asset pipeline completion

### Goal
Replace placeholder sensory feedback with a coherent Tecmo-like presentation layer.

### Remaining work
1. **Sound effects hookup**
   - whistle, snap, tackle, catch, incomplete, turnover, touchdown, menu navigation
2. **Music support**
   - title/menu/game over or intentionally scoped ambient-only fallback
3. **Crowd / ambience**
   - ambient loops and event reactions
4. **Asset completeness audit**
   - faces, menu assets, field tiles, playbook imagery, palette mappings, sprite coverage
5. **Content validation tooling**
   - verify YAML/data coverage against disassembly source assets and references

### Acceptance
Core game events produce correct feedback and the asset pipeline supports the intended feature set without placeholder gaps.

---

## Phase 7 — determinism, replay, and parity-grade validation

### Goal
Make progress measurable and safe as feature scope expands.

### Remaining work
1. **Headless scenario suite**
   - scripted scenarios for run, pass, fumble, interception, punt, kickoff, field goal, touchdown, turnover on downs, halftime, endgame
2. **Replay/event recording**
   - enough structured output to debug regressions and support post-play summaries
3. **NES/disassembly comparison tools**
   - trace/behavior comparison where feasible for routes, timing, pursuit feel, and asset mapping
4. **Regression gates**
   - build + scenario pack + data validation as standard acceptance for major gameplay changes

### Acceptance
Major gameplay systems can be changed without guessing whether they broke football state.

---

## Recommended execution order

1. **Phase 1: full scrimmage football loop**
2. **Phase 3: game-state/rules/scoreboard completeness** (in parallel where possible with Phase 1)
3. **Phase 2: special teams and scoring paths**
4. **Phase 4: UI and presentation**
5. **Phase 7: validation and determinism hardening** (should grow alongside phases 1-4, not wait until the end)
6. **Phase 6: audio and asset completion**
7. **Phase 5: season/save/meta-game**

## Immediate implementation slices to dispatch now

1. **Scrimmage gap audit → first implementation backlog**
   - enumerate exact missing pass/block/turnover/reset cases in current SimArch systems
2. **Special teams architecture pass**
   - map kickoff/punt/field-goal support already present vs missing in current systems/content
3. **Rules/scoreboard full-game audit**
   - verify what is missing for full-game clock, possession, scoring, and stats correctness
4. **UI/front-end plan**
   - define minimal path from title/menu/team-select into a playable exhibition flow
5. **Season/save/meta audit**
   - turn `SAVE_SEASON_DESIGN.md` into an executable backlog ordered behind exhibition completeness
6. **Audio/content audit**
   - identify which cues/assets are placeholders and which content banks already support hookup
7. **Validation expansion**
   - design the next headless scenarios beyond the current 2-play smoke test
