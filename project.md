# Tecmo Super Bowl MonoGame - Project

## Overview
MonoGame reimplementation of Tecmo Super Bowl NES, preserving original gameplay mechanics while using modern C# patterns.

**Repository:** https://github.com/Montblanc-mogbot/tecmo-super-bowl-monogame
**Design Doc:** `docs/DESIGN.md`

---

## Active Work

### Phase 1: Core Framework #nextaction

- [ ] **Review scope and ask Matt clarifying questions** #nextaction
  - Confirm project structure approach
  - Discuss target platform(s)
  - Clarify content loading strategy (load all at startup vs on-demand)
  - Confirm MonoGame version target
  - Any specific tooling preferences?

- [ ] **Project setup** #nextaction
  - Create MonoGame project structure
  - Set up solution and project files
  - Configure YAML content pipeline
  - Add YamlDotNet dependency

- [ ] **Content pipeline (YAML loading)** #nextaction
  - Implement YamlContentLoader
  - Create ContentManager wrapper
  - Test loading existing YAML files (teams, plays, formations)
  - Error handling for missing/invalid YAML

- [ ] **Basic entity system** #nextaction
  - Create Entity base class
  - Implement Component system
  - Create Player and Ball entities
  - Set up BehaviorComponent for AI

- [ ] **Rendering pipeline** #nextaction
  - Set up SpriteBatch configuration
  - Create FieldRenderer
  - Create PlayerRenderer
  - Camera/viewport management
  - 256x224 base resolution with scaling

---

## Completed

- [x] All 32 NES banks scaffolded as YAML
- [x] All C# model and loader classes created
- [x] DOCS files processed
- [x] DESIGN.md created with architecture specification

---

## Notes

**Gameplay Mechanics (Preserved from NES):**
- Tecmo-style velocity: no momentum, instant direction changes
- Discrete collision: frame-by-frame distance checks
- Behavior stack: push/pop for grapple interrupts
- Rating-driven outcomes: HP, RS, MS determine success

**Key Data Files:**
- `content/teamtext/bank16_team_text_data.yaml` - 28 NFL teams
- `content/playcall/playlist.yaml` - Complete play list
- `content/formations/formation_data.yaml` - Offensive formations
- `content/constants/tecmo_constants.yaml` - Game constants
