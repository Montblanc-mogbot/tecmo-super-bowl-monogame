# QB AI (Tecmo-style) - Data Format Notes

## DESIGN.md reference
`docs/DESIGN.md` currently does **not** document Tecmo ROM-accurate QB dropback/read logic (no `dropback`, `scramble`, or `read progression` sections). The QB AI implementation in code therefore uses *deterministic approximations* and expects explicit YAML to be added.

## Required YAML to become data-driven
The QB brain is seeded by `PlaySpawner` today. For real Tecmo authenticity we should add explicit per-play QB AI fields to a playdata YAML file.

### Proposed schema (per offensive play)
```yaml
qb_ai:
  dropback: five_step        # shotgun|three_step|five_step|seven_step|rollout_left|rollout_right
  read_order: [WR1, WR2, TE, RB]  # slots, not entity ids
  read_time_limit_frames: 45      # optional override; default 45
  pressure_threshold_frames: 30   # optional override; default 30
  throw:
    min_open_frames: 4            # don't throw on 1st open frame
    break_window_radius_px: 4     # route break window radius around first breakpoint
```

### Mapping `read_order` to entities
- `WR1`, `WR2`, `TE`, `RB` refer to `PlayerRoleComponent.Slot` (case-insensitive)
- If a slot is missing in a formation, it is skipped
- Any remaining eligible receivers (WR/TE/RB) may be appended deterministically by entity id (optional)

### Route break signal
Until we import ROM timing tables, "throw on break" is approximated as:
- receiver is within `break_window_radius_px` of their **first route waypoint** (`OffensiveAssignmentComponent.RouteWaypoints[0]`)

Long-term, playdata should include explicit break frames per route segment.

## Current code defaults
- Dropback defaults to `FiveStep` unless the play name/slot includes shotgun/rollout/"3"/"7" hints.
- Read order defaults to `WR1 -> WR2 -> TE -> RB -> remaining eligibles`.
