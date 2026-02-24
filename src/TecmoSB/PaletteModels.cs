namespace TecmoSB;

public sealed record Palette(
    string Id,
    IReadOnlyList<string> Colors
);

public sealed record PaletteCycle(
    string Id,
    IReadOnlyList<string> Frames,
    int TicksPerFrame
);
