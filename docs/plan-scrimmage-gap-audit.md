# Scrimmage gap audit backlog

Updated: 2026-05-07

## Summary

Current SimArch scrimmage is a working vertical slice, but the standard-play loop is still incomplete in a few structural ways:
- passing resolves as offense-only catch-or-dead-ball, with no real incompletion/interception/return path
- QB decisioning is placeholder and not driven by per-play reads, pressure, or defensive leverage
- blocking and coverage are scaffolded, but still heuristic rather than play-data/assignment complete
- fumbles exist only as a debug/manual path; tackle-driven turnovers and loose-ball spot/reset rules are incomplete
- play-end/reset and down-distance can advance a basic tackle result, but they do not yet robustly cover multi-drive pass/turnover/scoring scenarios
- headless validation is still a single 2-play smoke test, so repeated-drive corruption risks are largely unguarded

The original disassembly confirms the missing areas are first-class runtime responsibilities, especially in:
- `Bank19_20_on_field_gameplay_loop.asm` for turnover-on-downs, touchdowns/safeties, and fumble recovery/spotting
- `Bank21_22_play_commands_on_field_logic.asm` for fumble checks, interception flow, coverage timing, and defender reaction windows
- `Bank5_6_off_def_play_data.asm` for explicit block/pass-rush/play-command intent that should feed SimArch behaviors more directly

## Highest-leverage backlog

### 1. Pass outcome completion and post-catch/turnover control
**Why first:** current passing path is the biggest rules hole and blocks meaningful multi-drive validation.

**Current gaps**
- `src/TecmoSBGame/SimArch/Systems/PassFlightCompleteSystem.cs`
  - only searches offensive players inside radius
  - marks ball `Held` by nearest offense or `Dead`
  - does not publish/record catch, incompletion, interception, or return-specific results
- `src/TecmoSBGame/SimArch/Systems/PassFlightStartSystem.cs`
  - flight setup is deterministic but does not persist passer/target metadata into `Ball.PasserEntityId` / `Ball.TargetEntityId`
- `src/TecmoSBGame/SimArch/Systems/QbAiSystem.cs`
  - fixed read order, fixed dropback timer, no pressure/scramble behavior, no play-specific read map
- `src/TecmoSBGame/SimArch/Sim.cs`
  - tackle-to-play-end flow is the only fully wired result path; pass completion/incompletion/interception does not yet integrate into lifecycle and rules strongly enough

**Target files**
- `src/TecmoSBGame/SimArch/Systems/PassFlightStartSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/PassFlightCompleteSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/QbAiSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/PlayResultResolver.cs`
- `src/TecmoSBGame/SimArch/Sim.cs`
- `src/TecmoSBGame/SimArch/Components/Ball.cs`

**Implementation slices**
1. Persist pass metadata on launch (`PasserEntityId`, `TargetEntityId`, `PassType`) and use it during resolution.
2. Replace offense-only eligible search with nearest eligible offense + nearest defender contest evaluation.
3. Resolve three explicit outcomes: catch, incomplete, interception.
4. On interception, transfer possession to defender and let play continue until tackle/score/out-of-bounds instead of dead-balling immediately.
5. On incomplete, immediately produce a post-play result with zero/negative? yards handled correctly and no possession swap.
6. Feed resolved pass outcomes into play/lifecycle state so post-play summary and down-distance reflect the actual outcome.
7. Extend QB read selection to prefer actual route runners and skip covered/invalid reads before forcing the next read.

**Acceptance criteria**
- a pass can end as catch, incompletion, or interception deterministically
- intercepted balls become live returns with defender possession until tackled/whistled
- incomplete passes enter post-play without corrupting control, ball ownership, or next-play reset
- pass results update `PlayState.Result`, `WhistleReason`, and `MatchState` consistently over repeated plays

---

### 2. Blocking assignment parity and rush-vs-protection outcomes
**Why second:** passing and outside runs will stay brittle until trench behavior is more stable.

**Current gaps**
- `src/TecmoSBGame/SimArch/Systems/BlockerAiSystem.cs`
  - nearest-defender lane heuristic only
  - no explicit use of ROM block target opcodes beyond coarse initial assignment
  - no double-team, win/lose, shed, or pocket integrity consequences
- `src/TecmoSBGame/SimArch/Systems/PlayScriptSystem.cs`
  - supports a minimal subset of script ops; blocking intent is not deeply reflected in runtime behavior
- `src/TecmoSBGame/SimArch/Spawning/FormationSpawner.cs`
  - offense gets generic `BlockTarget` defaults, defense gets generic rush/coverage defaults
- disassembly references in `Bank5_6_off_def_play_data.asm`
  - explicit `blockPlayer`, `block`, and `passRush` commands exist and should map more directly to assignments and releases

**Target files**
- `src/TecmoSBGame/SimArch/Systems/BlockerAiSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/EngagementSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/DefensiveRushSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/PlayScriptSystem.cs`
- `src/TecmoSBGame/SimArch/Spawning/FormationSpawner.cs`
- `src/TecmoSBGame/SimArch/Spawning/PlaySpawner*.cs`

**Implementation slices**
1. Preserve per-player block intent from play data when applying a play, not just generic slot defaults.
2. Add blocker/rusher win probability and disengage logic tied to ratings/time-in-engagement.
3. Support stable pocket-preserving pass-block landmarks so rushers cannot instantly collapse on every dropback.
4. Add selective double-team/second-level climb behavior based on assignment rather than fixed timeout alone.
5. Ensure rush wins influence QB pressure flags/read timing.

**Acceptance criteria**
- offensive line targets are explainable from play data/slot responsibility
- rushers can win, be stalled, or be redirected deterministically
- QB pocket time changes based on protection quality rather than a fixed dropback timer
- repeated scrimmage plays do not leave blockers/rushers stuck in engagement state across resets

---

### 3. Coverage/read interaction hardening
**Why third:** pass completeness depends on defenders reacting credibly to routes and throws.

**Current gaps**
- `src/TecmoSBGame/SimArch/Systems/CoverageSystem.cs`
  - has a useful scaffold, but landmarks/reaction windows are generated heuristically per defender
  - no strong mapping from defensive play call to actual man/zone responsibilities in SimArch runtime
  - in-air behavior always breaks toward ball endpoint, without contested-catch nuance
- `src/TecmoSBGame/SimArch/Spawning/FormationSpawner.cs`
  - defenders currently default to generic `ZoneHook` plus placeholder rush values
- disassembly in `Bank21_22_play_commands_on_field_logic.asm`
  - explicit coverage delays and tighter/looser coverage branches indicate timing/spacing should be more data-driven

**Target files**
- `src/TecmoSBGame/SimArch/Systems/CoverageSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/QbAiSystem.cs`
- `src/TecmoSBGame/SimArch/Spawning/FormationSpawner.cs`
- `src/TecmoSBGame/SimArch/Systems/PlayCall/PlayCallSystem.cs`
- any defense play application path used in `PlaySpawner` / formation script setup

**Implementation slices**
1. Map defensive call metadata into concrete `Coverage.Type`, landmarks, and assignment targets at play application time.
2. Distinguish tighter vs looser man leverage/reaction timing based on play call or ratings.
3. Feed coverage state into QB read scoring so heavily covered receivers are deprioritized.
4. During pass resolution, let nearby defenders influence catch probability and interception chance rather than teleporting all in-air breaks equally.

**Acceptance criteria**
- different defensive calls produce visibly different coverage shells/responsibilities
- QB read progression changes when first read is covered
- coverage timing/leverage affects catch vs breakup vs interception outcomes deterministically

---

### 4. Real fumble lifecycle and turnover spot handling
**Why fourth:** the reset path cannot be trusted until non-debug turnovers are real.

**Current gaps**
- `src/TecmoSBGame/SimArch/Systems/FumbleDebugSystem.cs`
  - only debug/manual fumble trigger exists
- `src/TecmoSBGame/SimArch/Systems/TackleResolutionSystem.cs`
  - no live `ShouldForceFumble` path; current code comments this out
- `src/TecmoSBGame/SimArch/Systems/LooseBallPickupSystem.cs`
  - pickup works, but no special spot/ownership rules for out-of-bounds or recovery team
- `src/TecmoSBGame/SimArch/Systems/PlayResultResolver.cs`
  - only resolves tackle end spots
- `src/TecmoSBGame/SimArch/Systems/DownDistanceSystem.cs`
  - turnover handling is generic and does not cover nuanced fumble/touchback/return spot cases
- disassembly in `Bank19_20_on_field_gameplay_loop.asm` and `Bank21_22_play_commands_on_field_logic.asm`
  - includes explicit forward-OOB fumble spot correction, own/opp recovery branches, touchback handling, and recovery-team resolution

**Target files**
- `src/TecmoSBGame/SimArch/Systems/TackleResolutionSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/FumbleDebugSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/LooseBallPickupSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/PlayResultResolver.cs`
- `src/TecmoSBGame/SimArch/Systems/DownDistanceSystem.cs`
- `src/TecmoSBGame/SimArch/Sim.cs`

**Implementation slices**
1. Introduce a real deterministic fumble chance on tackle/contact using carrier/tackler ratings.
2. Capture initial fumble spot and current loose-ball spot so forward-out-of-bounds can be corrected.
3. Let loose-ball recovery continue play only when appropriate; otherwise whistle and finalize turnover/retained possession.
4. Add touchback/end-zone recovery handling.
5. Ensure recovery swaps control safely and updates `PlayState.Result.Turnover` only when possession truly changes.

**Acceptance criteria**
- live tackles can cause fumbles without debug input
- offense-own recovery, defense recovery, and out-of-bounds fumbles all resolve to deterministic next states
- turnover and spot rules survive consecutive drives without corrupting `BallSpot`, `PossessionTeam`, or `DriveId`

---

### 5. Play-end/reset reliability across non-run outcomes
**Why fifth:** once pass and turnover paths exist, reset orchestration must be hardened.

**Current gaps**
- `src/TecmoSBGame/SimArch/Systems/PlayLifecycleSystem.cs`
  - increments `MatchState.PlayNumber` on `StartNewPreSnap`, while `DownDistanceSystem` also increments play count on play end
  - phase changes are clean, but duplicated play-number authority is a likely multi-drive bug source
- `src/TecmoSBGame/SimArch/Systems/NextPlayResetSystem.cs`
  - resets dynamic components well, but only spots the ball and clears state; it does not re-anchor players/formations for changed possession/direction cases yet
- `src/TecmoSBGame/SimArch/Systems/PlayResultResolver.cs`
  - no centralized resolution for incomplete pass, interception end, fumble end, score, safety, touchback
- `src/TecmoSBGame/SimArch/Sim.cs`
  - tackle whistle path manually invokes play-end and down-distance; other result paths are not symmetrically integrated

**Target files**
- `src/TecmoSBGame/SimArch/Systems/PlayLifecycleSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/NextPlayResetSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/PlayResultResolver.cs`
- `src/TecmoSBGame/SimArch/Sim.cs`
- `src/TecmoSBGame/SimArch/Systems/PreSnapSystems.cs`
- `src/TecmoSBGame/SimArch/State/MatchState.cs`
- `src/TecmoSBGame/SimArch/State/PlayState.cs`

**Implementation slices**
1. Make `MatchState.PlayNumber` advancement single-owner.
2. Centralize play-end publication for tackle, incomplete, interception return end, fumble recovery end, touchdown, safety, touchback.
3. Ensure reset path reestablishes correct offense/defense direction, control owner, ball owner, and pre-snap alignment after possession changes.
4. Add assertions/logging around impossible state combinations (`PreSnap` with held live ball, duplicate play ids, etc.).

**Acceptance criteria**
- no duplicated or skipped play ids across repeated drives
- every whistle/end reason converges through one consistent post-play/reset path
- possession changes fully reset control/alignment without leaking prior-play state

---

### 6. Deterministic multi-drive validation expansion
**Why last in this track but should grow continuously:** without this, regressions will be hard to trust.

**Current gaps**
- `src/TecmoSBGame/SimArch/SimArchHeadless.cs`
  - only runs one 2-play scenario and checks tick advancement
  - no assertions for down/distance, possession, scoring, turnovers, or reset correctness

**Target files**
- `src/TecmoSBGame/SimArch/SimArchHeadless.cs`
- `src/TecmoSBGame/SimArch/Headless/HeadlessRunner.cs`
- new scenario/helper files under `src/TecmoSBGame/SimArch/Headless/` or `tests/`

**Implementation slices**
1. Extract scenario harness helpers for scripted inputs/play selections and state assertions.
2. Add dedicated scenarios for:
   - completed pass then tackle
   - incomplete pass
   - interception and return tackle
   - forced fumble and same-team recovery
   - forced fumble and turnover recovery
   - touchdown drive and reset to next possession
   - turnover on downs over a multi-play drive
3. Assert `Down`, `YardsToGo`, `BallSpot`, `PossessionTeam`, `DriveId`, `PlayId`, and `Phase` after each scenario.

**Acceptance criteria**
- headless suite covers at least run, catch, incompletion, interception, fumble, touchdown, and turnover-on-downs
- scenarios are deterministic and runnable from the existing validation loop
- failures identify which state transition broke

## Recommended execution order

1. Pass outcome completion and post-catch/turnover control
2. Coverage/read interaction hardening
3. Blocking assignment parity and rush-vs-protection outcomes
4. Real fumble lifecycle and turnover spot handling
5. Play-end/reset reliability across non-run outcomes
6. Deterministic multi-drive validation expansion

## Small safe first coding slice recommendation

I did **not** implement code in this pass.

The safest first slice appears to be:
- **persist pass metadata in `PassFlightStartSystem` and consume it in `PassFlightCompleteSystem`**

Why this slice is safe:
- it is self-contained within the passing track
- it does not require choosing final turnover/reset architecture yet
- it reduces ambiguity for the next pass-resolution work without broad cross-track conflicts

Suggested micro-acceptance for that slice:
- when a pass starts, `Ball.PasserEntityId`, `Ball.TargetEntityId`, and `Ball.PassType` are populated deterministically
- pass completion code reads those fields instead of relying only on proximity heuristics
