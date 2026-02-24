# Tecmo Super Bowl MonoGame - Project Reference

**GTD Brain:** `Projects/tectonic-super-bowl-clone/project.md`  
**Repository:** https://github.com/Montblanc-mogbot/tecmo-super-bowl-monogame  
**Design Doc:** `docs/DESIGN.md`

---

## Overview
MonoGame reimplementation of Tecmo Super Bowl NES, preserving original gameplay mechanics while using modern C# patterns.

---

## Status

**Phase:** 1 - Core Framework  
**Active work tracked in:** `Projects/tectonic-super-bowl-clone/project.md`

---

## Completed

- Scaffolded all 32 NES banks as YAML with C# models/loaders
- Processed all DOCS files
- Created DESIGN.md with gameplay mechanics specification

---

## Reference

**Gameplay Mechanics:**
- Tecmo-style velocity: no momentum, instant direction changes
- Discrete collision: frame-by-frame distance checks
- Behavior stack: push/pop for grapple interrupts
- Rating-driven: HP, RS, MS determine outcomes

**Key Data Files:**
- `content/teamtext/bank16_team_text_data.yaml` - 28 NFL teams
- `content/playcall/playlist.yaml` - Complete play list
- `content/formations/formation_data.yaml` - Offensive formations
- `content/constants/tecmo_constants.yaml` - Game constants
