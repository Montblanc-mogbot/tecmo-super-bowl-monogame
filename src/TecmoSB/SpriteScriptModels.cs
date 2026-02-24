namespace TecmoSB;

public sealed record SpriteScript(
    string Id,
    IReadOnlyList<SpriteFrame> Frames,
    SpriteLoop? Loop
);

public sealed record SpriteFrame(
    int Duration,
    IReadOnlyList<SpritePiece> Sprites,
    SpriteOp? Op
);

public sealed record SpritePiece(
    string Tile,
    int X,
    int Y,
    int Pal = 0,
    bool FlipX = false,
    bool FlipY = false
);

public sealed record SpriteOp(string Kind, int Dx = 0, int Dy = 0);

public sealed record SpriteLoop(string Kind, int Frame);
