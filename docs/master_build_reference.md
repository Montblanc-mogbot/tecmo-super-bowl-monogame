# Master Build Reference

Source: `master_build.asm`

## What This File Is

The main build file for the NES ROM assembly. It:
- Includes all bank files
- Sets up the ROM header
- Defines the build order
- Configures memory mapping

## Contents

```asm
; Typical structure:
.INCLUDE "constants_variables/..."
.INCLUDE "macros/..."

; Bank includes
.BANK 0
.ORG $8000
.INCBIN "Bank1_2_team_data.asm"

.BANK 1
...

; Reset vectors
.BANK 31
.ORG $FFFA
.DW NMI_VECTOR
.DW RESET_VECTOR
.DW IRQ_VECTOR
```

## For MonoGame

**Not applicable** - This is purely a build orchestration file for the 6502 assembler.

### MonoGame Equivalent

| master_build.asm | MonoGame |
|------------------|----------|
| `.INCLUDE` | `using` statements, project references |
| `.BANK` | Content folders |
| `.ORG` | Not needed |
| `.INCBIN` | `Content.Load<>()` |
| Reset vectors | `Program.cs`, `Game1.cs` |
| ROM header | Not needed |

## Project Structure Comparison

### NES (master_build.asm)
```
master_build.asm
├── Bank1-32 .asm files
├── constants_variables/
├── macros/
└── Output: TSB.nes ROM
```

### MonoGame (C# Project)
```
TecmoSB.csproj
├── src/TecmoSB/*.cs
├── content/*/*.yaml
├── Content.mgcb
└── Output: .exe + Content/
```

## Implementation

The MonoGame "master build" is:

1. **TecmoSB.csproj** - MSBuild project file
2. **Program.cs** - Entry point (replaces reset vector)
3. **Game1.cs** - Main game class
4. **Content.mgcb** - Content pipeline for assets

## Notes

- Original: Single assembly file builds entire ROM
- MonoGame: Standard .NET build process
- No YAML needed - standard C# project structure
