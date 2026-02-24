namespace TecmoSB;

public sealed record SpriteScriptBankIndex(
    string Bank,
    IReadOnlyDictionary<int, string> Scripts
);
