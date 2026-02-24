namespace TecmoSB;

public sealed record MenuScript(
    string Id,
    IReadOnlyList<MenuOp> Ops
);

public sealed record MenuOp(
    string Kind,
    IReadOnlyDictionary<string, string> Args
);

public sealed record MenuScriptIndex(
    string Bank,
    IReadOnlyDictionary<int, string> Scripts
);
