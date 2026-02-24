# Sprite Scripts (Tecmo) → YAML plan

Bank9/Bank10 in the disassembly are largely **data banks**: “sprite scripts” (metasprite scripts) and associated tile data.

## What we need in MonoGame

### 1) Data model (YAML)
We want authored data that describes:
- a named script (`id`)
- frames/steps (sequence)
- per-step: which sprites to draw (tile id, palette, flip flags, x/y offsets)
- per-step: optional control ops (delay, loop, goto, hide)

### 2) Runtime interpreter
A small VM that:
- advances per fixed tick
- emits a set of `SpriteDrawCommand`s each frame
- supports looping and simple branching

### 3) Rendering
- Map `SpriteDrawCommand` → MonoGame `SpriteBatch.Draw` (eventually via a texture atlas)

## Proposed YAML (v0)

```yaml
id: nfl_shield_scroll_down
origin: { x: 0, y: 0 }
frames:
  - duration: 4
    sprites:
      - { tile: shield_tl, x: 0,  y: 0,  pal: 0 }
      - { tile: shield_tr, x: 8,  y: 0,  pal: 0 }
      - { tile: shield_bl, x: 0,  y: 8,  pal: 0 }
      - { tile: shield_br, x: 8,  y: 8,  pal: 0 }
  - duration: 4
    op: { kind: moveOrigin, dx: 0, dy: 1 }
    sprites: [ ... ]
loop:
  kind: loopTo
  frame: 0
```

## Notes
- The *feel* is driven by timing (durations) and coordinate quantization.
- Initially we can render tiles as placeholders and focus on script timing.
