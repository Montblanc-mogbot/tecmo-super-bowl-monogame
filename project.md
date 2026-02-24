# Tecmo Super Bowl MonoGame - Project

**Location:** `repos/tecmo-super-bowl-monogame/project.md`  
**Repository:** https://github.com/Montblanc-mogbot/tecmo-super-bowl-monogame  
**Design Doc:** `docs/DESIGN.md`

---

## Phase 1: Core Framework #nextaction

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

- [x] Scaffolded all 32 NES banks as YAML with C# models/loaders
- [x] Processed all DOCS files
- [x] Created DESIGN.md with gameplay mechanics specification

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
