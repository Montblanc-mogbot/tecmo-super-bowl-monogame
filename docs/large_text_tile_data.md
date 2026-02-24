# Large Text Tile Data

Source: `large_text_tile_data.asm`

## What This File Is

Contains tile data for large text characters used in the game UI:
- Team names (large display)
- "TECMO BOWL" logo text
- Menu headers
- Stat titles

## Contents

The file defines 2x2 tile (16x16 pixel) characters for:
- A-Z letters (large font)
- Numbers 0-9 (large font)
- Special characters

## For MonoGame

Instead of NES tile patterns, use:
- **SpriteFont** or **TrueType fonts** for text
- **Texture2D** for any special graphical text
- **UI rendering** at higher resolution

## Implementation

No YAML needed - standard MonoGame text rendering replaces this:

```csharp
// Instead of tile data, use:
spriteBatch.DrawString(font, "TECMO BOWL", position, color);
```

## Notes

- Original: Custom 16x16 tile patterns stored in CHR ROM
- MonoGame: Standard font rendering or high-res textures
- File size: ~6KB of tile pattern data
