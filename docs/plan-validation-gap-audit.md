# Validation / Determinism Gap Audit

Updated: 2026-05-07

## Scope

This audit covers the current SimArch headless, replay, and determinism-validation story and turns the remaining gaps into a concrete backlog for scenario coverage and regression gating.

Reference inputs:
- `docs/FULL_COMPLETION_PLAN.md` phase 7 + related phases 1-3
- current runtime/headless entrypoints in `src/TecmoSBGame` and `src/Sim/TecmoSBHeadless`
- original-game feature surface represented in the completion plan (scrimmage, special teams, scoring, clock, halftime/endgame, replay/parity)

## Current state summary

The project has a real but still narrow deterministic-validation foundation:

1. **A minimal SimArch smoke test exists and runs**
   - `dotnet run --project src/TecmoSBGame -- --headless-2plays 240`
   - today this only checks that a deterministic scrimmage bootstrap advances ticks and returns a 22-player snapshot
   - the current pass condition is very weak: the run I inspected printed repeated handoff logs and still passed because the only enforced invariant is `Snapshot.Tick > 0`

2. **A richer legacy-style headless harness exists in a separate console project**
   - `src/Sim/TecmoSBHeadless/Program.cs`
   - supports `--scenario kickoff|presnap`, forced TD/safety hooks, and event logging for pass/fumble/pickup/whistle paths
   - this is the strongest current validation surface, but it is still a manual console harness, not a regression suite with assertions and named expected outcomes

3. **Replay types exist, but SimArch replay capture is not actually implemented**
   - `src/TecmoSBGame/SimArch/Replay/*` defines capture models and disk flushing
   - `src/TecmoSBGame/SimArch/Systems/ReplayRecorderSystem.cs` is still TODO and records nothing

4. **NES trace comparison exists only as a scaffold**
   - `src/TecmoSBGame/SimArch/Headless/NesTraceCompareRunner.cs`
   - currently just loads JSON and reruns the two-play smoke path; it does not diff aligned snapshot fields

5. **Deterministic simulation intent is strong in code structure**
   - fixed-timestep updates, deterministic tie-breaks, seeded resolution helpers, explicit next-play reset systems
   - but **determinism is mostly claimed structurally, not verified by repeatable scenario gates**

## What is missing versus the original/full-game feature surface

Compared to the completion-plan feature surface, current validation only lightly covers one tiny scrimmage slice. There is little or no regression coverage for:

- standard scrimmage outcomes: complete pass, incomplete pass, interception, fumble + recovery, first down, turnover on downs, touchdown, safety
- repeated-drive integrity: down/distance, possession, spotting, kickoff-after-score, next-play reset across many plays
- special teams: kickoff return outcomes, punt flow, field goal / extra point, touchback/out-of-bounds variants
- full-game rules: quarter rollover, halftime side/possession transitions, endgame, scoreboard correctness, stats accumulation
- parity-grade evidence: replay snapshots, structured event traces, NES/disassembly behavioral comparison

## Audit findings by area

### 1) Headless scenario coverage

**What exists**
- SimArch CLI only exposes `--headless-2plays`
- separate console harness supports `kickoff` and `presnap`
- forced TD / safety hooks exist in the separate harness

**Main gaps**
- no first-class named scenario pack in the main validation loop
- no assertions for expected football-state outcomes
- no multi-play drive scenarios with pass/fail criteria
- no scenario coverage for punts, field goals, PATs, halftime, or endgame
- no repeated-run determinism check (same scenario twice => identical structured output)

**Impact**
- gameplay changes can easily regress rules or state transitions without tripping CI/manual acceptance

### 2) Replay / structured event recording

**What exists**
- replay capture schema (`ReplayCapture`, `ReplayFrame`, `ReplayBallState`)
- recorder file output utility

**Main gaps**
- SimArch replay recorder system is a stub
- current snapshot/replay schema is too thin for useful football debugging
- no standardized per-tick/per-event artifact emitted by headless runs
- no post-play event summary artifact for assertions

**Impact**
- failures are hard to diff
- deterministic-repeat verification has no canonical artifact to compare
- future post-play summaries/stats hooks lack a dependable data feed

### 3) NES/disassembly comparison

**What exists**
- trace JSON models
- compare-runner scaffold
- notes acknowledging snapshot parity is not ready

**Main gaps**
- no stable mapping from SimArch state to comparable trace fields
- no per-frame diffing, tolerance policy, or scenario-specific comparison tooling
- no curated comparison scenarios (routes, dropback timing, pursuit, kickoff lane flow, etc.)

**Impact**
- “parity” remains qualitative/manual rather than measurable

### 4) Determinism verification itself

**What exists**
- deterministic coding style across many systems
- fixed-step architecture and explicit tie-break comments

**Main gaps**
- no test that re-runs the same scenario twice and proves byte-for-byte equivalent replay/event output
- no test for determinism across reset boundaries or multi-score transitions
- no audit gate for unstable iteration sources beyond comments/manual care

**Impact**
- determinism regressions can sneak in even if single runs look plausible

### 5) Regression gating and developer workflow

**What exists**
- build + `--headless-2plays` smoke check in docs/tasks
- YAML content validation at load time

**Main gaps**
- no scenario matrix that major gameplay changes are expected to pass
- no standard output contract for headless success/failure
- no distinction between smoke, rules, and parity suites
- no documented “minimum gate by change type” policy

**Impact**
- acceptance is still mostly ad hoc; large gameplay work has weak protection

## Recommended backlog

Order is based on leverage: get useful assertions and artifacts first, then broaden scenario surface, then add parity tooling.

### A. Foundation: make validation artifacts real

1. **Implement SimArch replay capture for real**
   - finish `SimArch/Systems/ReplayRecorderSystem.cs`
   - capture at minimum: tick, ball state/owner/position, all player positions, play phase, whistle/result state
   - wire optional replay capture into headless scenario runs

2. **Define a stable structured headless result format**
   - emit a concise per-scenario JSON summary with:
     - scenario id
     - seed
     - ticks run
     - final match state
     - final play state
     - key events encountered (snap, handoff, pass thrown, catch, INT, fumble, pickup, whistle, score, reset)
   - keep console logs, but make JSON the assertion target

3. **Add repeat-run determinism verification**
   - for a given scenario + seed, run twice and compare replay/event JSON byte-for-byte
   - fail if output diverges

### B. Replace the current smoke test with a real scrimmage scenario pack

4. **Upgrade `--headless-2plays` from “ticks advanced” to football assertions**
   - assert snap occurred
   - assert handoff occurred exactly once
   - assert ball ownership changed as expected
   - assert play reached whistle/post-play
   - assert next-play reset advanced cleanly

5. **Create first named scrimmage scenarios with explicit expected outcomes**
   - `scrimmage-run-basic`
   - `scrimmage-pass-complete`
   - `scrimmage-pass-incomplete`
   - `scrimmage-interception`
   - `scrimmage-fumble-offense-recovers`
   - `scrimmage-fumble-defense-recovers`
   - `scrimmage-first-down`
   - `scrimmage-turnover-on-downs`
   - `scrimmage-touchdown`
   - `scrimmage-safety`

6. **Add multi-play drive scenarios**
   - scripted 4-8 play drive with mixed outcomes
   - assertions for down/distance, possession, score, ball spot, and reset after each play

### C. Cover special teams and score transitions

7. **Expand kickoff scenarios beyond current kickoff slice smoke**
   - kickoff caught + returned
   - kickoff touchback
   - kickoff out of bounds
   - kickoff after touchdown
   - safety punt / safety transition if that scope stays in plan

8. **Add punt validation scenarios**
   - punt snap/kick/return
   - punt downed
   - punt touchback
   - muff/loose-ball recovery once mechanics exist

9. **Add field-goal / PAT scenarios**
   - made field goal
   - missed field goal
   - blocked field goal if in scope
   - extra point success/failure path

### D. Full-game rules validation

10. **Add clock / quarter scenarios**
   - live-play clock runoff
   - out-of-play no-run cases as rules mature
   - end-of-quarter rollover
   - halftime transition
   - end-of-game termination

11. **Add scoreboard / possession scenarios**
   - possession flip after interception/fumble turnover/downs
   - score transition into next possession type
   - side-direction correctness after halftime or rule-driven changes

12. **Add stats/event-capture readiness checks**
   - once stats exist, assert key counters for passing/rushing/turnovers/scoring
   - until then, keep scenario summaries ready to extend without format churn

### E. Parity-grade tooling

13. **Make NES trace compare actually diff structured state**
   - define a minimal aligned field set first: ball carrier, major actor positions, ball position, high-level phase/event markers
   - support scenario-specific tolerances where pixel-perfect parity is unrealistic early

14. **Create curated parity scenarios**
   - route timing / break timing
   - QB dropback and throw timing
   - defender pursuit / coverage reactions
   - kickoff lane behavior

### F. Regression gate policy

15. **Document and enforce three gate tiers**
   - **smoke**: build + one fast scenario pack
   - **rules**: scrimmage + score-transition + special-teams scenarios
   - **parity/dev**: replay diff + NES trace comparison where available

16. **Tie change types to minimum required gates**
   - play scripts/routes/QB AI/blocking/coverage => smoke + relevant scrimmage scenarios + determinism repeat check
   - kickoff/punt/FG work => smoke + relevant special-teams scenarios
   - clock/down-distance/scoreboard => rules pack including multi-play and quarter scenarios

## Suggested near-term execution slices

If this track is split into small implementation tasks, the best sequence is:

1. implement real SimArch replay/event capture
2. upgrade `--headless-2plays` assertions so it validates football state, not just tick advancement
3. add 3 high-value named scenarios first:
   - complete drive-reset run play
   - interception turnover
   - touchdown -> kickoff-after-score transition
4. add repeat-run determinism diff on those scenarios
5. then broaden into kickoff/punt/field-goal/full-game packs

## Bottom line

The project already has the beginnings of a deterministic validation architecture, but today it is still **smoke-oriented and scaffold-heavy**, not yet a trustworthy regression gate for the original game’s feature surface. The highest-value next move is to convert existing headless scaffolding into **asserted named scenarios with structured replay/event artifacts**, then grow coverage outward from scrimmage to score transitions, special teams, and full-game rules.
