using System;
using TecmoSB;

namespace TecmoSBGame.State;

/// <summary>
/// Shared, read-mostly view of the authoritative YAML-defined loop machines.
///
/// This is intended for legible system gating (systems can early-out based on the
/// current loop state) while we keep the ECS system graph stable.
/// </summary>
public sealed class LoopState
{
    public LoopState(GameLoopMachine gameLoop, OnFieldLoopMachine onFieldLoop)
    {
        GameLoop = gameLoop ?? throw new ArgumentNullException(nameof(gameLoop));
        OnFieldLoop = onFieldLoop ?? throw new ArgumentNullException(nameof(onFieldLoop));

        // Seed public snapshot fields.
        SyncFromMachines();
    }

    public GameLoopMachine GameLoop { get; }
    public OnFieldLoopMachine OnFieldLoop { get; }

    public string GameLoopId { get; private set; } = string.Empty;
    public string GameLoopStateId { get; private set; } = string.Empty;
    public int GameLoopTick { get; private set; }

    public string OnFieldLoopId { get; private set; } = string.Empty;
    public string OnFieldStateId { get; private set; } = string.Empty;
    public int OnFieldTick { get; private set; }

    public float GameLoopSecondsInState { get; private set; }
    public float OnFieldSecondsInState { get; private set; }

    /// <summary>
    /// Advances per-tick time counters and re-syncs snapshot fields.
    /// Call this once per fixed simulation tick.
    /// </summary>
    public void Advance(float fixedDtSeconds)
    {
        // Detect transitions and update timers.
        var prevGame = GameLoopStateId;
        var prevOnField = OnFieldStateId;

        SyncFromMachines();

        if (!string.Equals(prevGame, GameLoopStateId, StringComparison.Ordinal))
            GameLoopSecondsInState = 0f;
        else
            GameLoopSecondsInState += fixedDtSeconds;

        if (!string.Equals(prevOnField, OnFieldStateId, StringComparison.Ordinal))
            OnFieldSecondsInState = 0f;
        else
            OnFieldSecondsInState += fixedDtSeconds;
    }

    public bool IsOnField(params string[] stateIds)
    {
        for (var i = 0; i < stateIds.Length; i++)
        {
            if (string.Equals(OnFieldStateId, stateIds[i], StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private void SyncFromMachines()
    {
        var gl = GameLoop.Snapshot();
        GameLoopId = gl.ConfigId;
        GameLoopStateId = gl.CurrentStateId;
        GameLoopTick = gl.TickCount;

        var of = OnFieldLoop.Snapshot();
        OnFieldLoopId = of.ConfigId;
        OnFieldStateId = of.CurrentStateId;
        OnFieldTick = of.TickCount;
    }
}
