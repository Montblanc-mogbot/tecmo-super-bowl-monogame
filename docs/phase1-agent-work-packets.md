# Phase 1 agent work packets

Updated: 2026-05-07

This file turns the first four ordered tasks in `OPENCLAW_TASKS.md` into bounded agent-ready packets.

## Packet A — pass-state bookkeeping foundation

### Maps to task
- Task 1: Fix pass-state bookkeeping so pass outcomes can be resolved authoritatively.

### Goal
Persist enough pass metadata at throw start that pass completion logic can resolve outcomes without depending on transient or implicit state.

### Primary target files
- `src/TecmoSBGame/SimArch/Systems/PassFlightStartSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/PassFlightCompleteSystem.cs`
- likely one or more files under:
  - `src/TecmoSBGame/SimArch/Components/`
  - `src/TecmoSBGame/SimArch/State/`
  - `src/TecmoSBGame/SimArch/Events/`
- headless coverage files under:
  - `src/TecmoSBGame/SimArch/Headless/`
  - or whichever current scenario harness owns pass assertions

### Required reading
- `openclaw.md`
- `OPENCLAW_TASKS.md`
- `/home/montblanc/.openclaw/workspace/Projects/tectonic-super-bowl-clone/context.md`
- `docs/FULL_COMPLETION_PLAN.md`
- `docs/plan-scrimmage-gap-audit.md`

### Scope
- Identify what pass state currently gets lost between throw start and throw completion.
- Add the smallest durable metadata model needed to carry:
  - passer identity
  - intended receiver identity or slot
  - targeting / throw context needed by completion logic
  - enough defender/context linkage to avoid implicit inference where possible
- Update pass completion logic to consume the new authoritative metadata.
- Add or update a headless scenario with explicit assertions.

### Non-goals
- Do not fully implement interceptions/returns here unless they are trivial fallout from the state model.
- Do not refactor unrelated scrimmage systems.

### Acceptance
- Pass completion logic no longer relies on fragile implicit state for receiver/throw resolution.
- The new metadata is clearly owned and cleaned up by the correct lifecycle.
- A deterministic pass scenario asserts the expected metadata-driven result path.

### Validation
- `dotnet build src/TecmoSB.sln`
- run a targeted headless passing scenario with assertions
- if no dedicated pass scenario exists yet, add the smallest one that proves the new metadata survives throw start → completion

---

## Packet B — pass outcomes: completion / incompletion / interception

### Maps to task
- Task 2: Implement real incompletion, interception, and interception-return outcomes for SimArch passing.

### Depends on
- Packet A merged or otherwise available in the working tree.

### Goal
Replace the current offense-catch-or-dead-ball path with explicit pass result branches and correct play-state transitions.

### Primary target files
- `src/TecmoSBGame/SimArch/Systems/PassFlightCompleteSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/PlayEndSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/DownDistanceSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/PlayLifecycleSystem.cs`
- related event/state/component files as needed
- headless scenario files

### Required reading
- same core files as Packet A
- plus relevant original references mentioned in `docs/plan-scrimmage-gap-audit.md`

### Scope
- Add explicit pass outcomes for:
  - completion
  - incompletion
  - interception
  - interception return / immediate post-interception live-ball handling if the current architecture supports it cleanly
- Ensure possession/state transitions are authoritative.
- Ensure dead-ball vs live-ball behavior is explicit and deterministic.
- Add targeted scenario coverage for all three main result classes.

### Non-goals
- Do not deepen all QB AI / coverage heuristics here.
- Do not tackle full scoreboard/season stats here unless required to keep state correct.

### Acceptance
- A deterministic pass scenario can finish as completion, incompletion, or interception with the correct owner and play-end behavior.
- Possession changes survive into the next state correctly.

### Validation
- `dotnet build src/TecmoSB.sln`
- run targeted headless passing scenarios covering completion, incompletion, interception
- verify repeat-run identical outcomes for the new scenarios

---

## Packet C — QB / coverage interaction hardening

### Maps to task
- Task 3: Harden QB AI, receiver progression, and coverage interaction around real pass outcomes.

### Depends on
- Packet B or equivalent pass outcome support.

### Goal
Make pass results driven more by game state and defensive interaction, less by loose heuristics.

### Primary target files
- `src/TecmoSBGame/SimArch/Systems/QbAiSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/CoverageSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/ManCoverageSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/ZoneCoverageSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/RouteFollowSystem.cs`
- any pass-target selection or related component/state files

### Required reading
- same core files as Packet A
- `docs/plan-scrimmage-gap-audit.md`
- `docs/rom-command-mapping.md`
- `docs/QB_AI.md`

### Scope
- Inspect current QB read / throw timing behavior.
- Tighten pass-target selection and defender interaction enough to exercise:
  - coverage win
  - pressure-influenced timing
  - defender ball-play opportunities
- Prefer bounded improvements that create deterministic, testable scenarios.

### Non-goals
- Do not attempt perfect Tecmo parity in one pass.
- Do not refactor all route data unless required for scenario correctness.

### Acceptance
- Named pass scenarios can demonstrate different outcomes due to coverage and timing, not just random or implicit behavior.
- Results are repeatable across runs.

### Validation
- `dotnet build src/TecmoSB.sln`
- run expanded headless pass scenarios
- repeat the same scenario multiple times and confirm identical outputs

---

## Packet D — blocking / rush / engagement hardening

### Maps to task
- Task 4: Deepen blocking, rush, and engagement resolution beyond current heuristic scaffolding.

### Goal
Improve line-play fidelity enough that blocking and rush materially affect run/pass outcomes.

### Primary target files
- `src/TecmoSBGame/SimArch/Systems/BlockerAiSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/EngagementSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/DefensiveRushSystem.cs`
- `src/TecmoSBGame/SimArch/Systems/MovementSystem.cs`
- relevant components under `src/TecmoSBGame/SimArch/Components/`
- `docs/BLOCKING.md`

### Required reading
- same core files as Packet A
- `docs/BLOCKING.md`
- `docs/plan-scrimmage-gap-audit.md`

### Scope
- Improve block assignment fidelity where current nearest-lane approximations are too weak.
- Deepen engage/disengage behavior.
- Make pass-rush pressure more meaningful to the scrimmage loop.
- Preserve determinism and avoid broad unrelated architecture churn.

### Non-goals
- Do not try to solve every defensive AI gap here.
- Do not fold in full special-teams blocking.

### Acceptance
- Blocking/rush interaction has visible gameplay consequences in both run and pass scenarios.
- Pressure can influence offensive outcomes in deterministic tests.

### Validation
- `dotnet build src/TecmoSB.sln`
- run existing scrimmage scenarios
- add at least one pressure-focused or blocking-focused headless scenario with assertions

---

## Dispatch recommendation

Use these in dependency order:
1. Packet A
2. Packet B
3. Packet C
4. Packet D

To reduce conflicts:
- Packet A and Packet D can be explored in parallel if Packet D stays away from pass outcome files.
- Packet B should wait for Packet A.
- Packet C should wait for Packet B.

## Common validation rule

Every packet must leave behind:
- passing `dotnet build src/TecmoSB.sln`
- either a new asserted headless scenario or an expanded existing asserted scenario
- concise notes on what state is now authoritative and what still remains
