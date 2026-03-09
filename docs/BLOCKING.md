# Blocking (Tecmo-style) - Implementation Notes

This project’s current formation YAML is derived from the NES command scripts.
Those scripts contain explicit blocking opcodes (e.g. `Block-NT`, `Block-RE`, `PassBlock`, `SetToBlock`).

## BLOCK-1: Assignment format (from formation YAML)

In `content/formations/formation_data.yaml`, each offensive player slot has a `commands` string.
Blocking intent appears as:

- `SetToBlock(FF E0)` – sets the player into a blocking mode in the original engine.
- `PassBlock` – pass protection routine.
- `Block-<DEFENDER>` – man assignment vs a specific defensive slot, examples:
  - `Block-NT`, `Block-LE`, `Block-RE`
  - `Block-RILB`, `Block-LILB`, `Block-ROLB`, `Block-LOLB`
  - `Block-RCB`, `Block-LCB`

### Current ECS translation

We do not yet model exact defender “slot” identities (RE/NT/etc) as first-class ECS data.
So we translate the above into **high-level assignments** (`BlockAssignmentType`):

- `Block-*` or `PassBlock` => `ManOn` (select nearest eligible defender in-lane)
- `SetToBlock` without an explicit `Block-*` => `GapLeft`/`GapRight` (heuristic by OL side)

This translation happens in `FormationSpawner.TryGetInitialBlockAssignment`.

## Engagement trigger distance (pixels)

The NES disassembly notes indicate engagement triggers around **4–6 pixels**.

In the MonoGame implementation we use:

- `BlockerAISystem.CONTACT_DISTANCE_PIXELS = 6f`
- `EngagementSystem` re-checks the true distance on `BlockContactEvent` and requires distance <= 6px.

(We keep `CollisionContactSystem`’s broader proximity scan, then gate to the final distance in `EngagementSystem` for determinism.)

## Double-team formation rules

In Tecmo, double-teams occur when **two blockers engage the same defender**.

Current implementation (`BlockerAISystem.ApplyDoubleTeams`):

- If >= 2 blockers report `IsEngaged` with the same `EngagedEntityId`:
  - both blockers are marked `IsDoubleTeam=true`
  - defender receives a heavy speed penalty via `SpeedModifierComponent`
    - multiplier: `0.45`
    - refreshed while double-teamed

Additional effects (increasing block win chance / reducing break chance) are planned but not yet modeled because the grapple/block win resolver is still scaffolded.

## YAML gaps / next steps

- Defense entities spawned today only have broad roles (`DL/LB/DB`) and an optional debug `Slot` string.
  The `Block-RE/NT/...` exact man assignments are therefore approximated by lane/nearest selection.
- No explicit run play “pull” opcodes are present in the current YAML sample; `PullLeft/PullRight` are reserved for future parsing.
- There is no explicit LOS / gap geometry yet; gap assignments are currently a Y-offset heuristic.
