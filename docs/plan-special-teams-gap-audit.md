# Special teams and scoring-path gap audit

Updated: 2026-05-07

## Scope

This audit covers the current **SimArch** status for:
- kickoff
- punt
- field goal
- PAT / extra point
- post-score transitions

It compares the current repo state against:
- `docs/FULL_COMPLETION_PLAN.md`
- the legacy ArchiveMge implementation/scaffolding
- the original disassembly bank layout in `/home/montblanc/repos/Tecmo_Super_Bowl_NES_Disassembly`

## Executive summary

Current status: **kickoff has a minimal partial slice; punt/FG/PAT are mostly content-only or scaffold-only in SimArch; score-transition support is incomplete.**

What exists today:
- SimArch can run standard scrimmage plays.
- SimArch has a minimal kickoff flight start/completion path.
- SimArch parses special-teams formation tokens (`Punt`, `FieldGoalTakeSnap`, `FieldGoal`).
- SimArch includes a field-goal block-rush scaffold.
- Content already contains kickoff, punt, FG/XP formations and field-goal worksheet data.
- ArchiveMge already contains richer kickoff behavior and punt/FG scaffolds that can be ported.

What is still missing in SimArch:
- no kickoff setup/spawn/return/coverage system parity with ArchiveMge
- no kickoff-after-score transition system wired into SimArch rules/lifecycle
- no punt snap/kick/flight/return/downing/touchback flow
- no field goal snap/hold/kick/result flow
- no PAT decision/execution/post-score continuation flow
- no special-teams-specific deterministic scenarios in SimArch validation

## Evidence by area

### 1. Kickoff

#### Present in SimArch
- `Sim.cs` includes `KickoffFlightCompleteSystem`.
- `KickoffFlightStartSystem.cs` can start a deterministic kickoff-style ball flight.
- `KickoffFlightCompleteSystem.cs` only converts completed kickoff flight into a loose ball.
- `SimArch.State.MatchState.ResetForKickoff()` exists, but only stores kicking/receiving team metadata.
- `SimArch.Flow.GameFlowController` has a `Kickoff` state and kickoff-team recomputation.

#### Missing in SimArch
- no kickoff scenario spawner equivalent to ArchiveMge `GameStateSystem.SpawnKickoffScenario(...)`
- no kickoff setup phase / kick input / kick execution orchestration
- no kickoff return AI system port
- no kickoff coverage AI system port
- no touchback, out-of-bounds, or tackle-to-scrimmage bootstrap path in SimArch kickoff flow
- no `KickoffAfterScoreSystem` port despite SimArch event definitions already containing `KickoffSetupEvent`
- no kickoff-specific headless scenario in SimArch; existing kickoff scenario is still in legacy headless code

#### Comparison to original disassembly
The original game clearly treats kickoff as a full gameplay path with dedicated formation/play data and simulated kickoff logic:
- `Bank3_formation_metatile_data.asm` / `content/formations/formation_data.yaml` include kickoff formation 00
- `Bank5_6_off_def_play_data.asm` / `content/playdata/bank5_6_play_data.yaml` include kickoff reactions and returner behavior
- `Bank12_13_sim_update_stats.asm` contains dedicated kickoff distance/return logic, touchdown handling, and onside handling
- `Bank26_misc.asm` includes kickoff/onside recovery handling

Conclusion: current SimArch kickoff support is only a thin flight primitive, far short of the original bank-level feature area.

### 2. Punt

#### Present in SimArch
- formation parser recognizes `Punt`
- content includes punt formation (`formation 01`) and punt playdata category
- `SimArch/Systems/PuntCoverageSystem.cs` exists but is a TODO stub
- `SimArch/Systems/PuntReturnSystem.cs` exists but is a TODO stub

#### Available in ArchiveMge to port
- `ArchiveMge/Systems/PuntCoverageSystem.cs` has working scaffold behavior
- `ArchiveMge/Systems/PuntReturnSystem.cs` has working scaffold behavior
- ArchiveMge runtime wires these systems into the main game loop

#### Missing in SimArch
- no punt flight-start system
- no punt flight-complete system
- no punt-specific ball-flight kind/result handling
- no punt formation spawn/play-selection path
- no punt returner/coverage entity tagging in SimArch components/spawners
- no muff, loose-ball, downing, touchback, or out-of-bounds punt rules
- no possession/spot update after punt outcomes

#### Comparison to original disassembly
The original disassembly has explicit punt handling in both play data and sim/stats logic:
- punt command macros in `macros/play_data_macros.asm`
- punt decision/simulation in `Bank12_13_sim_update_stats.asm`
- punt touchback and punt-return logic in `Bank12_13_sim_update_stats.asm`
- punt recovery distinctions in `Bank26_misc.asm`

Conclusion: SimArch punt support is currently parser recognition plus TODO placeholders, not a playable path.

### 3. Field goal / PAT

#### Present in SimArch
- formation parser recognizes `FieldGoalTakeSnap` and `FieldGoal`
- `content/formations/formation_data.yaml` includes FG formation 02
- `content/fieldgoal/fg_worksheet.yaml` contains detailed FG success data
- `SimArch/Systems/FieldGoalBlockRushSystem.cs` exists as a minimal ported scaffold

#### Missing in SimArch
- no field-goal snap/hold/kick state machine
- no field-goal ball-flight start/completion/result system
- no use of `fg_worksheet.yaml` in gameplay execution
- no PAT/XP play selection or execution path
- no distinction between FG and XP distances/spotting despite `SimConfigYamlLoader` exposing `XpKickDistanceYards`
- no made/missed/blocked kick result handling
- no post-FG possession transition
- no post-XP kickoff sequencing

#### Comparison to original disassembly
The original game allocates real feature area here:
- dedicated FG/XP formation and commands in formation/play data banks
- recovery rules for blocked FG/XP in `Bank26_misc.asm`
- sim/stats and kicker result handling in `Bank12_13_sim_update_stats.asm`
- kicker/FG stats and leader logic in `Bank25_leaders_player_data_pro_bowl_abbrev.asm`

Conclusion: SimArch has the data prerequisites and one defensive scaffold, but essentially none of the gameplay path.

### 4. Post-score transitions

#### Present in SimArch
- `PlayLifecycleSystem` and match/play state models support play-end transitions in general.
- `SimArch.Events` already define `KickoffSetupReason` and `KickoffSetupEvent`.
- `MatchState` can store score and kickoff team metadata.

#### Missing in SimArch
- no SimArch equivalent of ArchiveMge `KickoffAfterScoreSystem`
- no score-to-kickoff automation after touchdown
- no safety-to-free-kick automation
- no PAT stage after touchdown
- no full possession/drive transition for special-teams scoring results
- no clear distinction between “scoring play ended” and “next state is PAT vs kickoff vs regular presnap”

#### Relevant ArchiveMge behavior
- `ArchiveMge/Systems/KickoffAfterScoreSystem.cs` already performs deterministic score→kickoff setup for TD/safety.
- `ArchiveMge/State/GameStateManager.cs` has explicit `PAT`, `ExtraPoint`, `FieldGoal`, and `Safety` flow helpers.

Conclusion: SimArch has event/model placeholders but not the rules pipeline that consumes them.

## Concrete backlog

### Priority 1 — kickoff parity baseline
1. **Port kickoff setup/transition orchestration into SimArch**
   - Add a SimArch `KickoffAfterScoreSystem` using existing `KickoffSetupEvent` definitions.
   - Make scoring plays publish/consume kickoff setup deterministically.
2. **Create kickoff slice spawner for SimArch**
   - Port the smallest useful subset of ArchiveMge `GameStateSystem.SpawnKickoffScenario(...)`.
   - Spawn ball, kicker/coverage unit, receiving unit, and kickoff-return tags/components.
3. **Port kickoff coverage and return systems**
   - Bring over ArchiveMge scaffold logic first, keeping behavior deterministic.
4. **Add kickoff outcome handling**
   - touchback
   - out-of-bounds
   - caught return
   - tackle / whistle spot
   - bootstrap back into scrimmage presnap state
5. **Add SimArch kickoff validation scenario**
   - one touchback path
   - one returned kickoff path
   - one post-score kickoff setup path

### Priority 2 — punt playable path
6. **Port punt coverage and punt return scaffolds to actual SimArch behavior**
   - current files are empty TODO shells; port ArchiveMge logic.
7. **Add punt flight systems**
   - start punt flight
   - complete punt flight
   - select returner / loose ball if needed
8. **Add punt special-teams spawn path**
   - punt formation selection/spawn
   - punt-team and return-team tagging
9. **Add punt result rules**
   - return
   - touchback
   - out of bounds / dead at spot
   - turnover of possession and correct next ball spot
10. **Add deterministic punt scenarios**
   - standard return
   - touchback
   - no-return/downed punt

### Priority 3 — field goal / PAT baseline
11. **Implement field-goal snap/hold/kick slice**
   - consume formation 02 and parsed FG ops
   - create a simple deterministic kick timing/result path
12. **Wire `fg_worksheet.yaml` into a gameplay rules helper**
   - start with made/missed calculation only
   - defer perfect timing/power-bar nuance if needed
13. **Port/use field-goal block rush scaffold in a real FG flow**
   - minimal first version can treat block rush as pressure/blocked outcome gate
14. **Add PAT / XP flow after touchdown**
   - after TD, branch to PAT state instead of direct kickoff
   - apply +1 / miss result
   - then trigger kickoff setup
15. **Explicitly defer or scope two-point conversion**
   - either implement a simple branch now or mark out of scope for this phase
16. **Add deterministic FG/PAT scenarios**
   - made FG
   - missed FG
   - made XP
   - missed/blocked XP if scoped

### Priority 4 — edge cases and parity hardening
17. **Safety and free-kick correctness audit**
   - ensure scoring team/receiving team are assigned correctly after safety
18. **Blocked-kick / muff / loose-ball recovery rules audit**
   - align with `Bank26_misc.asm` recovery distinctions
19. **Onside kick scope decision**
   - content/config exists (`onsides_kick_recovery.yaml`), but gameplay path is absent
   - either add a backlog slice or explicitly defer
20. **Stats integration**
   - kick return / punt return / punting / FG / XP stats accumulation

## Recommended first implementation slice

The safest non-overlapping first slice is:

**Port SimArch `KickoffAfterScoreSystem` and wire score→kickoff setup events into the SimArch rules flow.**

Why this first:
- tiny compared with full kickoff/punt/FG slices
- leverages event/types already present in SimArch
- directly closes a real scoring-transition gap called out in the completion plan
- low overlap with scrimmage mechanics and low asset risk
- creates the backbone needed before PAT/kickoff completion can behave correctly

## Why I did not implement code in this pass

I stayed read-only because there is not one obviously isolated gameplay slice that can be completed safely without also deciding:
- where SimArch kickoff entities are spawned/owned
- how SimArch scoring transitions should interleave with `PlayLifecycleSystem`
- whether the current SimArch host is expected to enter kickoff mode immediately or only after a dedicated kickoff slice exists

The best next coding task is still clear, but it should be done deliberately as a small focused implementation pass rather than as an opportunistic partial edit inside this audit.
