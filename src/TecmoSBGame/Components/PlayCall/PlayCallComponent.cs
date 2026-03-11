using System.Collections.Generic;

namespace TecmoSBGame.Components.PlayCall;

/// <summary>
/// Singleton-ish component holding the current playcall UI selection state.
/// Attach this to a single ECS entity.
/// </summary>
public sealed class PlayCallComponent
{
    public bool Visible;

    public PlayCallStep Step = PlayCallStep.Offense;
    public PlayCallFocus Focus = PlayCallFocus.Formation;

    // ---- Offense ----
    public readonly List<string> FormationIds = new();
    public int FormationIndex;

    public readonly List<TecmoSB.PlayEntry> PlaysForFormation = new();
    public int PlayIndex;

    // ---- Defense ----
    public readonly List<TecmoSB.DefensiveExecution> DefensiveCalls = new();
    public int DefenseIndex;

    // ---- Convenience/current selection (for rendering + spawners) ----
    public string SelectedFormationId = "";
    public TecmoSB.PlayEntry? SelectedPlay;
    public string SelectedDefenseId = "";

    // Auto-playcall guard (dev shortcut): prevent re-emitting selection every tick.
    public int LastAutoPlaycallPlayId = -1;

    // Used for edge detection inside systems that don't have per-entity input state.
    public bool PrevA;
    public bool PrevB;
    public bool PrevStart;
    public bool PrevSelect;
    public bool PrevUp;
    public bool PrevDown;
    public bool PrevLeft;
    public bool PrevRight;
}

public enum PlayCallStep
{
    Offense = 0,
    Defense = 1,
}

public enum PlayCallFocus
{
    Formation = 0,
    Play = 1,
    Defense = 2,
}
