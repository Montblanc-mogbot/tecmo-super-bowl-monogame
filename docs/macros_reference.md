# Macros Reference

Source: `macros/*.asm` files

## Overview

The `macros/` directory contains ~15 `.asm` files defining assembly macros used throughout the codebase.

## Macro Categories

### 6502/CPU Macros
- `6502_macros.asm` - General 6502 instruction helpers
- `nes_macros.asm` - NES-specific hardware macros
- `mmc3_macros.asm` - MMC3 mapper macros (bank switching)

### Game Logic Macros
- `math_macros.asm` - Arithmetic operations
- `check_status_macros.asm` - Player/status checking
- `play_call_macros.asm` - Play calling helpers
- `play_data_macros.asm` - Play data manipulation
- `player_ram_macros.asm` - Player data access

### Memory Macros
- `memory_save_load_clear_macros.asm` - SRAM operations
- `zero_page_variables.asm` - Zero-page access

### Input/Control Macros
- `joypad_macros.asm` - Controller reading
- `set_init_status_macros.asm`, `set_init_status_macros_2.asm` - Status setting
- `set_compare_player_ball_to_yardlines_macros.asm` - Field position checks

### Structural Macros
- `structure_macros.asm`, `struture_macros.asm` (typo in original) - Code structure
- `field_scroll_limit_macros.asm` - Screen scrolling
- `tecmo_macros.asm` - Game-specific helpers

## MonoGame Approach

### Convert to C# Helper Methods:
- `math_macros.asm` → C# Math/Calculation helpers
- `check_status_macros.asm` → C# Player state checks
- `play_call_macros.asm` → C# Play selection logic
- `field_scroll_limit_macros.asm` → Camera/scrolling logic

### Discard (NES-specific):
- `6502_macros.asm` - Not applicable
- `nes_macros.asm` - Hardware abstractions not needed
- `mmc3_macros.asm` - Bank switching not needed
- `memory_save_load_clear_macros.asm` - Use standard C# serialization
- `joypad_macros.asm` - Use MonoGame Input API
- `player_ram_macros.asm` - Use C# object properties

## Implementation

Most macro functionality maps to:

1. **C# Extension Methods** - For common operations on game objects
2. **Helper Classes** - Math, status checking, field position
3. **Service Classes** - Input handling, save/load
4. **Inline Code** - Simple operations don't need macros

## Examples

### Math Macro (Assembly)
```asm
.MACRO ADD_16BIT
    CLC
    ADC \1
    STA \2
.ENDM
```

### C# Equivalent
```csharp
// Simple operation - no macro needed
ushort result = (ushort)(a + b);
```

### Status Check Macro (Assembly)
```asm
.MACRO CHECK_PLAYER_STATUS
    LDA PLAYER_STATUS,X
    AND \1
.ENDM
```

### C# Equivalent
```csharp
// Extension method
public static bool HasStatus(this Player player, PlayerStatus status)
    => (player.Status & status) != 0;
```

## Files Breakdown

| File | Convert to C# | Discard | Notes |
|------|---------------|---------|-------|
| 6502_macros.asm | | ✓ | CPU instructions |
| check_status_macros.asm | ✓ | | Player state logic |
| field_scroll_limit_macros.asm | ✓ | | Camera bounds |
| joypad_macros.asm | | ✓ | Use MonoGame Input |
| math_macros.asm | ✓ | | Math helpers |
| memory_save_load_clear_macros.asm | | ✓ | Use standard IO |
| mmc3_macros.asm | | ✓ | Bank switching |
| nes_macros.asm | | ✓ | Hardware macros |
| play_call_macros.asm | ✓ | | Play selection |
| play_data_macros.asm | ✓ | | Play data access |
| player_ram_macros.asm | | ✓ | RAM access |
| set_compare_player_ball_to_yardlines_macros.asm | ✓ | | Field position |
| set_init_status_macros.asm | ✓ | | Status setting |
| set_init_status_macros_2.asm | ✓ | | Status setting |
| structure_macros.asm | | ✓ | Code structure |
| struture_macros.asm | | ✓ | Typo duplicate |
| tecmo_macros.asm | Partial | | Mixed content |

## Recommendation

Instead of direct translation:

1. **Implement game features** using standard C# patterns
2. **Reference these macros** only when specific behavior is unclear
3. **Create helper classes** organically as code grows
4. **Don't pre-emptively create macro libraries** - YAGNI principle
