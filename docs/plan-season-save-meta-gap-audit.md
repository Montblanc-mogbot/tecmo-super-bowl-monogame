# Season / Save / Meta Gap Audit

Updated: 2026-05-07

## Scope

This audit turns `docs/SAVE_SEASON_DESIGN.md` into a concrete implementation backlog, grounded in:
- the current MonoGame/SimArch repo state
- the original Tecmo source areas for season/static/meta systems
- the current completion plan ordering, which puts season/save/meta behind exhibition completeness

Relevant original-source modules reviewed:
- `Bank12_13_sim_update_stats.asm` — sim engine, season stat accumulation, overtime/game-end sim rules
- `Bank25_leaders_player_data_pro_bowl_abbrev.asm` — leaders screens, player data display, Pro Bowl abbreviations
- `Bank26_misc.asm` — schedule drawing, playoff-team reset/setup, roster/control screens
- `Bank27_misc.asm` — season/preseason CPU boost and misc meta/runtime support
- `Bank7_scene_scripts.asm` — scene-script primitives used by static/meta presentation
- `macros/memory_save_load_clear_macros.asm` — SRAM-oriented save/load helpers

## Current repo reality

## What exists
- Exhibition-oriented front-end flow exists: title → main menu → team select → coin toss → kickoff/on-field/post-play (`src/TecmoSBGame/SimArch/Flow/GameFlowController.cs`).
- Main menu already exposes `SEASON`, `PRO BOWL`, `OPTIONS`, `DATA` labels, but only `PRESEASON` actually transitions anywhere.
- Match state is still single-game scoped (`src/TecmoSBGame/SimArch/State/MatchState.cs`): score, clock, possession, down/distance, kickoff setup.
- Content loading already includes core team/team-text data and enough metadata to anchor future season references (`GameContent`, `TeamData`, `TeamTextData`).
- There is a minimal leaders data scaffold in YAML (`content/leaders/bank25_leaders_player_data_pro_bowl_abbrev.yaml`) and loader/models in `src/TecmoSB`, but it is not wired into runtime/UI.
- Replay scaffolding exists, which may later help game-result capture, but it is unrelated to persistence today.

## What does *not* exist yet
- No `SaveManager`, persistence service, save-path abstraction, or save-slot file IO.
- No season-domain models (`SeasonSave`, `SeasonTeam`, `SeasonSchedule`, `Standings`, `PlayoffBracket`, `Records`, etc.).
- No quick-save/resume support for mid-game state.
- No season-mode flow/controller/state in the runtime despite the older design doc mentioning it.
- No standings/schedule generation/simulation code.
- No leaders computation pipeline from accumulated stats.
- No season stat accumulation layer tied to completed games.
- No records/high-score persistence.
- No postseason or Pro Bowl state machine.
- No static/menu screens for schedule, standings, leaders, records, playoffs, roster browsing, or season summaries.

## Gap vs original source structure

### 1) Save / durable state
Original game relied on SRAM-oriented save/load/clear helpers and persistent season data layouts.

Current repo gap:
- zero persistence infrastructure
- zero schema/versioning strategy in code
- zero separation between global settings, slot saves, quick save, and records

Implication:
The current code cannot yet support even a single resumable season baseline.

### 2) Schedule / standings / playoff picture
Original source has explicit schedule rendering and playoff-related handling in `Bank26_misc.asm`, with season simulation/stat updates in `Bank12_13_sim_update_stats.asm`.

Current repo gap:
- no schedule representation
- no weekly progression model
- no played-vs-unplayed game list
- no standings or tiebreaker logic
- no playoff seeding/bracket data

Implication:
Even after full exhibition play works, there is no scaffolding to convert game outcomes into a season.

### 3) Season simulation and stat carry-forward
Original source includes skip-mode simulation and season stat update routines.

Current repo gap:
- no CPU-vs-CPU season sim service
- no game-result summary object that can be consumed by a season layer
- no canonical player/team stat containers for season-long accumulation
- no injury/condition season state

Implication:
The on-field engine can run plays, but there is no meta-layer contract for “game completed, update season now.”

### 4) Leaders / records / Pro Bowl
Original source dedicates major bank space to leaders/player-data/Pro Bowl display.

Current repo gap:
- leaders YAML is only a static scaffold
- no stat qualification rules
- no sorting/query service over player/team season stats
- no records book model
- no Pro Bowl roster selection logic or dedicated flow

Implication:
The repo has a content foothold here, but essentially none of the feature logic.

### 5) Season/static presentation screens
Original source uses scene/script infrastructure plus dedicated menu/static screens for schedule, roster, standings, leaders, and playoffs.

Current repo gap:
- current front-end stops at exhibition setup and on-field HUD
- no reusable season-menu navigation state
- no renderers for schedule/standings/leaders/playoff bracket/season summary pages

Implication:
Season work is blocked not just on data, but on a whole second UI surface area.

## Recommended implementation order

Because the full plan intentionally places season/save/meta after exhibition completeness, the best path is:
1. build persistence and season-domain scaffolding first
2. define the contract from completed exhibition/full games into season results
3. add schedule/standings progression
4. add sim + records/leaders
5. add playoffs/Pro Bowl
6. then flesh out presentation screens around those data models

## Concrete backlog

## Epic A — persistence foundation (must come first)

### A1. Save-path and file abstraction
- Create a save-root service with cross-platform location resolution.
- Support at minimum:
  - `settings.json`
  - `season_01.json` (one slot first)
  - `quicksave.json`
  - `highscores.json` or `records.json`
- Acceptance: code can create/read the save directory and round-trip a trivial JSON payload.

### A2. Versioned save envelope
- Add top-level save metadata shared by persisted files:
  - schema version
  - created/modified timestamps
  - game build/content version placeholder
- Acceptance: all persisted files deserialize through a versioned envelope.

### A3. `SaveManager` baseline
- Implement load/save/delete/exists for:
  - settings
  - one season slot
  - quick save
  - records/high scores
- Keep writes atomic (temp file + replace) to avoid corruption.
- Acceptance: a small test or harness round-trips each file type.

### A4. Global settings model
- Define actual settings payload instead of leaving `settings.json` abstract:
  - audio volumes/mute
  - input/preferences placeholders
  - presentation toggles as needed
- Acceptance: settings can be changed, saved, and reloaded without touching season data.

## Epic B — season domain model scaffolding

### B1. Core season aggregate types
Add explicit models for:
- `SeasonSave`
- `SeasonMetadata`
- `SeasonTeamState`
- `SeasonPlayerState`
- `SeasonSchedule`
- `SeasonWeek`
- `ScheduledGame`
- `SeasonStandings`
- `SeasonRecordBook`

Recommendation:
Keep these in a dedicated `Season/` or `Meta/` namespace instead of mixing them into `MatchState`.

### B2. Distinguish match state from season state
- Do **not** overload `MatchState` to be the season container.
- Introduce a `SeasonContext` or `SeasonSession` that owns:
  - current slot
  - current week
  - user team
  - references to schedule/standings/records
  - optional active game linkage
- Acceptance: the codebase has a clear boundary between one-game runtime state and durable season state.

### B3. Team/player identity mapping
- Define stable identifiers that bridge loaded content (`TeamData`, roster/player data) into season persistence.
- Audit whether current runtime has enough roster/player identity fidelity; today the spawner still leans heavily on placeholder/generated roster behavior.
- Acceptance: a persisted season can refer to teams/players by stable IDs, not transient entity IDs.

## Epic C — game-result capture contract

### C1. Canonical completed-game result model
Add a `GameResult`/`CompletedGameSummary` containing at minimum:
- home/away teams
- score
- possession-neutral winner/loser/tie
- per-team totals
- per-player stat lines that matter for season accumulation
- notable events for records/injuries if in scope

### C2. End-of-game export path from SimArch
- When full-game flow exists, emit a completed-game summary on final whistle.
- For now, structure the contract even if some fields are placeholders.
- Acceptance: season code can consume game results without reading live ECS/match internals.

### C3. Quick-save serialization boundary
- Decide what “mid-game resume” serializes:
  - full `MatchState`
  - selected play state
  - enough roster/player state to restore the running game
- This likely depends on exhibition/full-game completion first.
- Acceptance: explicit doc/code boundary for what quick save includes and what is deferred.

## Epic D — schedule and week progression

### D1. Schedule representation
- Implement a schedule model that supports:
  - regular season weeks
  - per-week game list
  - played/unplayed flags
  - scores/results
- Match the original 16-game/17-week structure before considering expansions.

### D2. Initial schedule source decision
Choose one of two paths:
1. hardcoded/imported canonical 1991-style schedules
2. generated schedules

Recommendation: start with deterministic imported schedules, not a generator. It is safer, simpler, and closer to the original game.

### D3. Week progression service
- Mark a game complete
- Advance current week when all required games resolve
- Support user-team play plus simming the rest
- Acceptance: a season can move from week N to N+1 deterministically.

### D4. Schedule screen backlog slice
- Add a read-only schedule screen first
- Then highlight current week / current user-team matchup
- Then add result markers and pagination

## Epic E — standings and playoff picture

### E1. Basic standings calculator
Implement baseline ordering for:
- wins/losses/ties
- points for/against
- division/conference buckets

### E2. Tiebreaker scope decision
Options:
1. minimal arcade ordering (win%, then PF differential)
2. richer NFL-like tiebreakers
3. exact original-game behavior if reverse-engineered

Recommendation: start with a documented minimal tiebreaker stack, then deepen later.

### E3. Playoff picture model
- division leaders
- wild cards
- seeding by conference
- eliminated/in-the-hunt status optional later

### E4. Standings/playoff UI screens
- standings grid by division/conference
- playoff picture summary
- separate bracket screen later

## Epic F — season simulation

### F1. Non-user game sim service
- Simulate scheduled games not played by the user.
- Produce the same `CompletedGameSummary` used by played games.
- Must be deterministic under a seed.

### F2. Season stat generation
- Roll up team and player output from simmed games.
- Keep this intentionally coarse at first; exact play-by-play parity is not required for v1.

### F3. Injury/condition scope decision
The design doc mentions injuries/condition “if in scope.”

Recommendation:
- v1: persist condition/injury fields in the schema, but keep logic minimal or disabled
- later: feed from played/simmed results

### F4. Sim-week workflow
- “Play user game, sim rest of week” should become the first complete season loop.
- Acceptance: one season can progress multiple weeks without manual intervention for all games.

## Epic G — leaders, records, and data screens

### G1. Expand the leaders content scaffold into a runtime service
- Wire `LeadersConfig` into actual leader queries.
- Map `stat_key` values to season stat selectors.
- Add qualification thresholds where needed (attempt minimums, etc.).

### G2. Season stat repository/query layer
- Provide reusable queries for:
  - top N passing/rushing/receiving/etc.
  - team offense/defense totals
  - player pages

### G3. Record book model
Track at minimum:
- single-game records
- season records
- maybe franchise/global highs depending on scope

### G4. Leaders/records screens
Recommended order:
1. simple text leaders page
2. records page
3. richer player-data presentation

### G5. Pro Bowl selection
- Select AFC/NFC rosters from season stats
- User override can be deferred
- Rendering/gameplay for an actual Pro Bowl matchup should come after selection/storage logic

## Epic H — postseason flow

### H1. Playoff bracket model
- wildcard
- divisional
- conference championship
- super bowl

### H2. Transition from regular season to playoffs
- determine qualifiers from standings
- seed bracket
- create playoff schedule objects

### H3. Postseason progression
- play/sim bracket games until champion
- persist champion and final standings/records

### H4. Bracket screen
- start read-only
- later add current-game highlighting and result advancement

## Epic I — season-mode front-end flow

### I1. Unlock `SEASON` menu item with real state transition
- Main menu currently displays the option but does nothing.
- Add a dedicated season-menu/controller state before any deep UI polish.

### I2. Season slot flow
Recommended first path:
- Season → New Season / Continue Season
- single slot first
- choose user team
- create season file
- enter season hub

### I3. Minimal season hub
First season hub should only need:
- current week + matchup
- schedule
- standings
- play game
- sim week / save / exit

### I4. Data menu reuse
The `DATA` main-menu item is a good home for leaders/records/player-data once those services exist.

## Suggested first executable slices

These are the safest, highest-leverage non-overlapping slices to implement after exhibition completeness is far enough along:

1. **Persistence skeleton**
   - save directory resolver
   - versioned JSON save helpers
   - `SaveManager` with settings + single season slot round-trip

2. **Season domain scaffolding**
   - `SeasonSave` + schedule/team/player/standings models
   - no UI yet

3. **Completed game summary contract**
   - define the object and where full-game flow will emit it

4. **Read-only season creation path**
   - main menu `SEASON` opens a placeholder season hub
   - create/load a single empty season slot

5. **Schedule + standings baseline**
   - imported schedule data
   - simple standings calculator
   - read-only screens

6. **Week progression + sim rest of week**
   - first complete “season loop” milestone

7. **Leaders/records service**
   - wire existing leaders YAML into computed rankings

8. **Playoffs / Pro Bowl**
   - only after weekly season progression is stable

## Risks and prerequisites the main agent should know
- This track is heavily blocked on full-game result correctness. Season scaffolding can start now, but trustworthy progression depends on completed game/end-state/stat export paths.
- Current runtime team/player identity is still thinner than a real season layer wants. Persisting transient/generated roster identities would create migration pain later.
- The original source splits season/meta behavior across simulation, UI, and SRAM-oriented banks; trying to recreate it as one giant feature would be risky. It should be decomposed into persistence, domain, progression, then presentation.
- The current repo already hints at season/pro-bowl/data menu surfaces, so a thin season-mode shell can be added without conflicting with scrimmage systems—but full implementation should stay clearly behind exhibition/full-game completeness.

## Tiny safe code slice?
No code change was made.

Reason: the task asked for the audit first, and the repo has no obvious tiny non-overlapping season/save/meta slice that is both useful and safe without choosing project structure/API names for persistence scaffolding. The best next step is to let the main agent pick the first backlog slice (recommended: persistence skeleton).