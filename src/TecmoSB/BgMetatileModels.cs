namespace TecmoSB;

/// <summary>
/// NES-era "metatile" concept: a 4x4 grid of 8x8 tiles (32x32 px) plus a packed attribute byte.
/// In the disassembly, entries are 17 bytes: 1 attribute byte + 16 tile bytes.
/// </summary>
public sealed record BgMetatile(
    string Id,
    int Attr,
    int[][] Tiles
);
