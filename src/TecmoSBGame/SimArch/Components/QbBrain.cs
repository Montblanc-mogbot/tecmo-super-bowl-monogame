namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Minimal QB AI state for SimArch.
///
/// This is a deterministic scaffold:
/// - drop back for N frames
/// - then attempt a pass to the current read target
/// </summary>
public struct QbBrain
{
    /// <summary>Frames remaining before first throw attempt.</summary>
    public int DropbackFramesRemaining;

    /// <summary>0-based index into the read order list.</summary>
    public int ReadIndex;

    /// <summary>Whether a pass has already been requested for this play.</summary>
    public bool PassRequested;

    /// <summary>Pass type preference (bullet/lob).</summary>
    public TecmoSBGame.SimArch.PassType PassType;
}
