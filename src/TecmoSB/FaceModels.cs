namespace TecmoSB;

public sealed record FaceDef(
    string Id,
    string SpriteScriptId
);

public sealed record FaceIndex(
    IReadOnlyDictionary<int, string> Faces
);
