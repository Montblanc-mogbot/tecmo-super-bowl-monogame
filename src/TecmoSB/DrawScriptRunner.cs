namespace TecmoSB;

/// <summary>
/// Very small interpreter scaffold for a DrawScript.
///
/// This does not render anything itself; it just calls an IDrawScriptSink with
/// normalized operations.
/// </summary>
public static class DrawScriptRunner
{
    public static void Run(DrawScript script, IDrawScriptSink sink)
    {
        foreach (var op in script.Ops)
        {
            sink.Handle(op);
        }
    }
}

public interface IDrawScriptSink
{
    void Handle(DrawOp op);
}
