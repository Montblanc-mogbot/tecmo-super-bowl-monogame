# Test Play Data Reference

Source: `DOCS/testdeforig.txt` and `DOCS/testofforig.txt`

## What These Files Are

Detailed test/debug play scripts for defensive (`testdeforig.txt`) and offensive (`testofforig.txt`) formations.

### Contents
- **testdeforig.txt**: ~3,000+ lines of defensive play tests
  - Multiple defensive formations (Defense: 13, 14, etc.)
  - Detailed player commands for each defender
  - Man-to-man coverage assignments
  - Zone drops and pass rush patterns

- **testofforig.txt**: Similar structure for offensive plays

## File Size
- testdeforig.txt: ~476KB
- testofforig.txt: ~123KB

## Structure Example
```
Defense: 13
RE: SetPosFromHike, 3pt stance, MoveAbsolute, Grapple, PassRush
NT: SetPosFromHike, 3pt stance, MoveAbsolute, Grapple, PassRush
...
```

## For MonoGame Implementation

These files contain **the same type of data** already captured in:
- `content/defenseplays/defextra.yaml`
- `content/defenseplays/bank4_defense_special_pointers.yaml`
- `content/offenseplays/offextra.yaml`
- `content/formations/formation_data.yaml`

### Recommendation

Rather than transcribing 3,000+ lines of test data:

1. **Use the scaffold YAML files** already created as templates
2. **Expand them gradually** with real play data as needed
3. **Reference these text files** for specific play behaviors when implementing

The test files are preserved in the disassembly repository for reference when specific play details are needed.

## Key Commands Found
- `SetPosFromHike` - Initial alignment
- `Grapple ALL` - Engage blockers
- `PassRush` - Rush the QB
- `m2m` (man-to-man) - Coverage assignment
- Complex zone drops with timing

These will map to the command system in `content/playdata/bank5_6_play_data.yaml`
