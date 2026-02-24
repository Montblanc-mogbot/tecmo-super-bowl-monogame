# TECMO PLAYBOOK HACKERS GUIDEBOOK

Source: `DOCS/TECMO PLAYBOOK HACKERS GUIDEBOOK ver2.0.doc`
Author: bruddog (Dave Brude)
Version: 2.0

## What This File Is

This is a ROM hacking guide for the original NES Tecmo Super Bowl. It documents:
- Hex offsets for team playbooks
- How to modify default playbooks using hex editors
- Tools for ROM hacking (TSBM, TsbPBE.exe, hex editors)
- Simulation editor information
- Play slot mappings and pointers

## Why It's Not Needed for MonoGame

This guide is specifically for **modifying the original NES ROM**. For the MonoGame reimplementation:

1. **No hex editing needed** - We use YAML data files and C# code
2. **No ROM offsets** - We have proper data structures
3. **No hacking tools** - We have content pipelines and editors
4. **Playbook data** - Already captured in `content/playdata/` and `content/formations/`

## MonoGame Equivalent

| ROM Hack Approach | MonoGame Approach |
|-------------------|-------------------|
| Hex edit at offset 0x1D310 | Edit `content/playdata/*.yaml` |
| TSBM tool | Direct YAML editing or future editor tool |
| TsbPBE.exe for play swapping | YAML configuration or in-game editor |
| Hex calculator | Float/int values in YAML |
| ROM simulation editor | C# simulation logic with tunable parameters |

## Relevant Content Already Ported

Playbook and formation data from this guide is already captured in:
- `content/formations/bank3_formation_metatile_data.yaml` - Formation definitions
- `content/playdata/bank5_6_play_data.yaml` - Play command scripts
- `content/playcall/bank20_playcall.yaml` - Play call menu structure
- `content/defenseplays/bank4_defense_special_pointers.yaml` - Defensive plays

## Original Guide Contents (for reference)

The guide covered:
1. Modifying team default playbooks (hex offset 0x1D310)
2. Play slot mappings for all 28 teams + Pro Bowl
3. Using TSBM 0.71 and 1.3 tools
4. Using TsbPBE.exe Beta 3 for play swapping
5. Using hex editors for manual changes
6. Simulation editor for stat tuning

## Conclusion

This document is **preserved for historical reference** but the actual data it describes has been restructured into the YAML data files in this repository. No direct port was necessary.
