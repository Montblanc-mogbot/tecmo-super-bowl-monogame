using System.Collections.Generic;

namespace TecmoSBGame.SimArch.Components.PlayCall;

/// <summary>
/// Singleton-ish component holding the current playcall UI selection state.
/// Attach this to a single entity.
///
/// Ported from: ArchiveMge/Components/PlayCall/PlayCallComponent.cs
/// </summary>
public struct PlayCallState
{
    public bool Visible;
    public bool WasVisible;

    public float DisplaySeconds;

    public PlayCallStep Step;
    public PlayCallFocus Focus;

    // ---- Offense ----
    public List<string> FormationIds;
    public int FormationIndex;

    public List<TecmoSB.PlayEntry> PlaysForFormation;
    public int PlayIndex;

    // ---- Defense ----
    public List<TecmoSB.DefensiveExecution> DefensiveCalls;
    public int DefenseIndex;

    // ---- Convenience/current selection ----
    public string SelectedFormationId;
    public TecmoSB.PlayEntry? SelectedPlay;
    public string SelectedDefenseId;

    public int LastAutoPlaycallPlayId;

    // Input edge tracking
    public bool PrevA;
    public bool PrevB;
    public bool PrevStart;
    public bool PrevSelect;
    public bool PrevUp;
    public bool PrevDown;
    public bool PrevLeft;
    public bool PrevRight;

    public static PlayCallState Default => new()
    {
        Visible = false,
        WasVisible = false,
        DisplaySeconds = 0f,
        Step = PlayCallStep.Offense,
        Focus = PlayCallFocus.Formation,

        FormationIds = new List<string>(),
        FormationIndex = 0,

        PlaysForFormation = new List<TecmoSB.PlayEntry>(),
        PlayIndex = 0,

        DefensiveCalls = new List<TecmoSB.DefensiveExecution>(),
        DefenseIndex = 0,

        SelectedFormationId = string.Empty,
        SelectedPlay = null,
        SelectedDefenseId = string.Empty,

        LastAutoPlaycallPlayId = -1,

        PrevA = false,
        PrevB = false,
        PrevStart = false,
        PrevSelect = false,
        PrevUp = false,
        PrevDown = false,
        PrevLeft = false,
        PrevRight = false,
    };
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
