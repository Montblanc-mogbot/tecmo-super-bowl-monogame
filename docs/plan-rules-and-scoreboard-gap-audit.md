# Rules / scoreboard / stats gap audit

Updated: 2026-05-07

## Scope

This audit covers the **SimArch** path for full-game rules correctness, specifically:
- clock and quarter flow
- possession and field-position state
- scoring updates and scoreboard/HUD correctness
- down/distance progression
- stat accumulation
- replay/post-play capture

Inputs reviewed:
- `openclaw.md`
- `OPENCLAW_TASKS.md`
- project context in `Projects/tectonic-super-bowl-clone/context.md`
- `docs/FULL_COMPLETION_PLAN.md`
- current `src/TecmoSBGame/SimArch` state/systems
- original source organization in `/home/montblanc/repos/Tecmo_Super_Bowl_NES_Disassembly`, especially `Bank12_13_sim_update_stats.asm`, `Bank17_18_main_game_loop.asm`, and `Bank19_20_on_field_gameplay_loop.asm`

## Executive summary

SimArch currently has **early scaffolding**, not full-game correctness, for this track.

What exists now:
- basic match state (`MatchState`) with score, quarter, clock, possession, down/distance, and ball spot
- basic play state (`PlayState`) with lifecycle phases and coarse play result fields
- minimal clock ticking during live play only
- minimal down/distance + scoring application on play end
- basic top/bottom HUD rendering from current match state
- post-play summary overlay heuristics
- replay model types plus a stub recorder system

What is still missing is the majority of the actual **game rules layer** that turns a playable scrimmage slice into a full Tecmo-style game. Compared to the original source layout, SimArch does **not yet have a true equivalent** of the Bank12/13 “update stats + clock + quarter + game bookkeeping” area; it has only thin fragments of that responsibility spread across a few small systems.

## Current SimArch coverage by area

### 1) Clock and quarter flow

Current coverage:
- `SimArch/Systems/GameClockSystem.cs`
  - clock only runs when `PlayState.Phase == InPlay`
  - decrements once per 60 ticks
  - advances quarter when clock reaches zero
  - ends match after 4th quarter
- `SimArch/State/MatchState.cs`
  - stores `Quarter`, `GameClockSeconds`, `MatchOver`

Major gaps:
- no distinction between clock **running**, **stopped**, or **play-select countdown** modes like the original main loop
- no play clock / between-play countdown behavior in SimArch state
- no end-of-half handling beyond “quarter increments”; halftime kickoff/side-change flow is absent
- no explicit quarter-start reset behavior besides resetting `GameClockSeconds`
- no special stop/run behavior for incomplete passes, out of bounds, scores, touchbacks, possession changes, etc.
- no late-game/2-minute behavior, no overtime decision, no explicit final whistle flow

Assessment vs original source:
- Original responsibilities are split across `Bank17_18_main_game_loop.asm` clock logic and `Bank12_13_sim_update_stats.asm` quarter/time updates.
- SimArch currently covers only the smallest possible “decrement a clock while in play” subset.

### 2) Possession and field-position rules

Current coverage:
- `MatchState` stores `PossessionTeam`, `OffenseDirection`, `BallSpot`, `KickingTeamIndex`, `ReceivingTeamIndex`
- `DownDistanceSystem` flips possession on generic turnover and resets down/distance
- `MatchState.SpotBallAbsoluteYard()` converts absolute yard to offense-relative spot
- `KickoffFlightCompleteSystem` turns completed kickoff flight into a loose ball

Major gaps:
- kickoff start states are not integrated into a full-game SimArch flow
- halftime receive/defer logic and side changes are absent
- turnover spot handling is oversimplified; return outcomes, end-zone turnover cases, and touchback nuances are missing
- safety continuation/free-kick implications are absent
- touchback handling is hardcoded to own 25 with no path-specific nuance
- no turnover-on-downs path in `DownDistanceSystem`
- offense direction is tied to team index after turnover, not to actual side/quarter progression rules
- no “change ends” quarter logic

Assessment vs original source:
- Original game bookkeeping mixes possession, clock, and scoreboard updates tightly across main-loop and stats/update banks.
- SimArch has isolated fields but not the orchestration needed for a real game.

### 3) Scoring updates and scoreboard correctness

Current coverage:
- `DownDistanceSystem` awards 6 for touchdown, 2 for safety
- `ScoreboardRenderer`, `DownDistanceRenderer`, and `Rendering/Hud/HudRenderer` display current state
- post-play overlay shows score-adjacent messaging heuristically

Major gaps:
- no PAT / extra point / two-point scoring integration
- no field goal scoring path in SimArch rules
- no kickoff-after-score orchestration in SimArch full-game flow
- no validation that HUD/scoreboard fields reflect authoritative state across score transitions
- score application is tied to coarse `PlayResult` booleans only; there is no richer scoring event model
- no end-of-game scoreboard/final-state presentation

Assessment vs original source:
- Original source has richer score-transition handling and scoreboard update logic around main game loop flow.
- SimArch HUD is present, but the underlying scoring model is still too thin for correctness.

### 4) Down and distance progression

Current coverage:
- `SimArch/Systems/DownDistanceSystem.cs`
  - increments play count
  - awards TD/safety points
  - flips possession on turnover
  - resets to 1st/10 after turnover
  - advances first down or increments down otherwise
  - spots ball using end absolute yard or touchback special case
- `MatchState.AdvanceDownDistance()` helper exists

Major gaps:
- no turnover on downs when failing on 4th down
n- no goal-to-go handling inside the 10
- `YardsToGo` logic is generic and can become inaccurate near goal line
- scoring plays do not drive the next legal game state; comment says “handled elsewhere,” but that full path is not in place
- no penalty-based first downs / replay of down / half-distance logic
- no explicit distinction between end-of-play result calculation and rules enforcement sequencing
- play numbering is likely double-counted: `PlayLifecycleSystem.StartNewPreSnap()` increments `MatchState.PlayNumber`, and `DownDistanceSystem.ApplyPlayEnd()` increments it again

Assessment vs original source:
- This is one of the clearest correctness gaps: SimArch has a starter resolver, but not a complete referee/rules engine.

### 5) Stats accumulation

Current coverage:
- effectively none in SimArch full-game state
- no per-player stat structures in `SimArch/State/MatchState.cs` or `PlayState.cs`
- no system equivalent to box-score accumulation
- docs recognize this as a remaining Phase 3/5 need

Major gaps:
- no passing stats: attempts, completions, yards, TDs, INTs
- no rushing stats: carries, yards, TDs
- no receiving stats: catches, yards, TDs
- no defensive stats: tackles, sacks, INTs, fumble recoveries, return TDs
- no kicking stats: PAT/FG attempts/makes, punts, return stats
- no team totals or leaders
- no end-of-game stat summary feed

Assessment vs original source:
- This is the biggest parity gap relative to `Bank12_13_sim_update_stats.asm`, whose responsibilities are almost entirely unported in SimArch.

### 6) Replay / post-play capture

Current coverage:
- `SimArch/Replay/ReplayModels.cs` defines a minimal capture format
- `SimArch/Replay/ReplayRecorder.cs` can reset and save JSON
- `SimArch/Systems/ReplayRecorderSystem.cs` is still a stub with `TODO`
- `PlayEndSystem` freezes state for post-play rendering
- post-play UI exists but uses heuristics rather than structured event data

Major gaps:
- no actual SimArch per-tick replay recording
- no event timeline for catches, tackles, turnovers, score, first down, out of bounds, etc.
- no structured post-play summary object; UI infers outcomes from `MatchState` and `PlayState`
- no replay capture of ball trajectory/entity positions despite existing models
- no integration between replay/event capture and future season/stats reporting

Assessment vs original source:
- SimArch has the shell of a replay format, but not the implementation.

## Concrete correctness risks already visible

1. **Play counter drift**
   - `PlayLifecycleSystem.StartNewPreSnap()` increments `MatchState.PlayNumber`
   - `DownDistanceSystem.ApplyPlayEnd()` also increments `MatchState.PlayNumber`
   - This likely causes play ids / play numbers to drift and will poison stats, replay naming, and validation if left unfixed.

2. **Offense direction oversimplification**
   - possession flips reset direction based on `newPossTeam == 0 ? LeftToRight : RightToLeft`
   - that is not enough for quarter-end side changes or full-game field orientation correctness.

3. **Clock semantics too thin**
   - running only during `InPlay` means there is no explicit stop/run policy by result type.
   - the current implementation cannot prove end-of-half/endgame correctness.

4. **Post-play UI depends on heuristics instead of authoritative event data**
   - first down and result messaging are inferred after the fact rather than driven by a resolved rules event.

5. **Replay/stat pipeline is disconnected**
   - models exist, but there is no actual capture or aggregation path.

## Recommended backlog order

### A. Build an authoritative full-game rules model first

1. **Introduce a richer match rules state model**
   - add explicit clock mode/state (stopped, running, play-select/between-play if needed)
   - add halftime/side-change state
   - add kickoff-after-score / start-of-half pending state
   - add possession-origin metadata needed for touchback/safety/turnover handling

2. **Refactor play-end resolution into explicit outcomes**
   - create a structured rules outcome object/event instead of only `PlayResult(bool Turnover, bool Touchdown, bool Safety)`
   - include: next possession, next spot, first down achieved, turnover-on-downs, score event type, clock behavior, kickoff required, auto-continue policy

3. **Fix play numbering ownership**
   - choose one authoritative place to increment play count/play id
   - ensure replay, headless validation, and stats all use the same source

### B. Complete down/distance + field position correctness

4. **Implement turnover-on-downs and goal-to-go rules**
   - 4th-down failure should change possession at the correct spot
   - yards-to-go inside the 10 should clamp to goal instead of always resetting to 10

5. **Separate spotting rules by outcome type**
   - tackle / OOB / incomplete / touchdown / safety / touchback / turnover return should each resolve through explicit spot logic

6. **Add quarter-end side-change support**
   - offense direction should be derived from full match context, not team index alone

### C. Finish clock/quarter orchestration

7. **Implement result-dependent clock behavior**
   - stop on incomplete, out of bounds, scores, change of possession where appropriate
   - confirm run/stop semantics around live-ball completion and post-play transition

8. **Add halftime and endgame transitions**
   - second-quarter end → halftime state and second-half kickoff setup
   - fourth-quarter end → final whistle/final state

9. **Add play clock / between-play timing only if needed for acceptance**
   - enough to support deterministic full-game simulation and eventual UX parity

### D. Create real stats accumulation

10. **Add box-score state types**
   - per-player and team stat aggregates in SimArch state/domain types

11. **Emit per-play stat events from resolved outcomes**
   - rushing, passing, receiving, defense, kicking
   - keep generation centralized so replay/season can reuse it

12. **Render/inspect stats in headless validation before UI polish**
   - prioritize deterministic assertions over visual-only integration

### E. Implement replay/event capture as a shared evidence layer

13. **Finish `ReplayRecorderSystem`**
   - record deterministic frames during `InPlay`
   - capture ball state + positions from world entities

14. **Add structured play event capture**
   - start snap, handoff, pass thrown, catch, tackle, turnover, score, whistle
   - use the same event payloads for post-play summary and future stats

15. **Replace post-play UI heuristics with resolved summary data**
   - first down / touchdown / turnover / incomplete should come from authoritative play-end summary data

## Suggested concrete implementation slices

1. **PlayNumber / PlayId correctness fix**
   - smallest safe rules bug to address first
   - prevents downstream replay/stat corruption

2. **Outcome-driven next-state resolver**
   - introduce a `ResolvedPlayOutcome` or similar model and route `DownDistanceSystem` through it

3. **Turnover-on-downs + goal-to-go support**
   - highest-leverage football correctness gap in current scrimmage-to-game transition

4. **Clock state machine upgrade**
   - add explicit stop/run transitions by whistle reason and play result

5. **Replay recorder implementation**
   - low-risk and useful for validating later rules changes

6. **Stats domain scaffolding + rushing/passing core stats first**
   - enough to start validating full-game accumulation

## Acceptance targets for this track

Before this phase can be called complete, SimArch should be able to prove:
- a game runs from opening kickoff to final whistle
- quarter transitions, halftime, and final state are correct
- possession and field direction remain correct across drives and halves
- scoreboard/HUD fields match authoritative rules state after every play
- down/distance and spot are correct for normal gains, first downs, turnovers, touchbacks, safeties, and scores
- replay or event capture exists to inspect state transitions deterministically
- per-game stats accumulate for core offensive/defensive/kicking categories

## Tiny safe slice recommendation

If coding immediately on this track, the safest non-overlapping first code slice is:
- **fix the SimArch play-number/play-id ownership bug**, then
- add a headless assertion that play ids advance exactly once per completed play.

That is small, local, and directly reduces future audit noise for rules/stats/replay work.
