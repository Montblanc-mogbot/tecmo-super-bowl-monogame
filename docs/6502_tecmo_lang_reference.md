# 6502 Tecmo Language Reference

Source: `DOCS/6502_tecmo_lang.xml`

## Overview

This is a Notepad++ User Defined Language (UDL) file providing syntax highlighting for Tecmo Super Bowl's 6502 assembly code. It's used for editor support when viewing/modifying the disassembly.

## Contents

### 6502 Opcodes (Keywords1)
Standard 6502 instructions:
- **Load/Store**: `LDA`, `STA`, `LDX`, `STX`, `LDY`, `STY`
- **Transfer**: `TAX`, `TAY`, `TXA`, `TSX`, `TXS`
- **Increment/Decrement**: `INX`, `DEX`, `INY`, `DEY`, `INC`, `DEC`
- **Flags**: `CLD`, `SEI`, `PHA`, `PLA`, `PLP`, `CLI`, `BIT`, `CLC`, `SEC`
- **Arithmetic**: `ADC`, `SBC`, `AND`, `ORA`, `EOR`, `CMP`, `CPX`, `CPY`
- **Shift/Rotate**: `LSR`, `ROL`, `ROR`, `ASL`

### Assembler Directives (Keywords2)
asm6f assembler commands:
- `.INCSRC`, `.INCBIN` - Include files
- `.ORG`, `.BASE` - Origin/base address
- `.DB`, `.DW`, `.WORD` - Data definitions
- `.dsb`, `.PAD` - Padding
- `.IF`, `.ELSE`, `.ELSEIF`, `.ENDIF` - Conditionals
- `.MACRO`, `.ENDM` - Macros
- `.ENUM`, `.ENDE` - Enumerations
- `.ERROR`, `.REPT`, `.ENDR`

### Game RAM Variables (Keywords4-5)
Extensive lists of game-specific variables including:
- Player data offsets (`P1_*`, `P2_*`)
- Game state variables (`BALL_*`, `CLOCK_*`, `DOWN`)
- Menu/Screen variables (`MENU_*`, `BG_*`)
- Sound engine variables (`SOUND_*`)
- SRAM save data (`SRAM_*`)
- Simulation variables (`SIM_*`)
- Playoff data (`AFC_*`, `NFC_*`)

### Folder Markers
- Open: `_F{`, `.{`
- Close: `_F}`, `.}`

## Usage in MonoGame Project

This file is **editor tooling** - not game data. For the MonoGame reimplementation:

1. **Opcodes**: Not needed - we're writing C#, not 6502
2. **Directives**: Not needed - we use YAML for data
3. **RAM Variables**: Reference for understanding original code, but we use:
   - `content/*/bank*.yaml` for game data
   - C# objects for runtime state (no RAM addresses)

## Notes

- Original file size: ~143KB
- Contains 500+ RAM variable names
- Useful for cross-referencing when porting logic
- Not converted to YAML - this is documentation only
