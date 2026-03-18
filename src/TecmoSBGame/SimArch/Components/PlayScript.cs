namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Arch sim playscript runtime state.
///
/// We keep this as a small unmanaged struct; the actual script op list will be referenced indirectly
/// (e.g., via an id into a script registry) to avoid storing managed references in components.
/// </summary>
public struct PlayScript
{
    public int ScriptId; // index into a script registry (TBD)
    public int Ip;

    public float WaitSeconds;

    // Handoff delay bookkeeping
    public int PendingHandoffToEntityId;
}
