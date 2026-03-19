using Arch.Core;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Bridges platform input into ECS components (MovementInput + PlayerActionState).
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/InputSystem.cs
/// </summary>
public sealed class InputSystem
{
    public void Update(World world)
    {
        // NOTE: MainGameArch currently pushes input directly into Sim.SetInput.
        // This system is scaffolded for parity and for future UI/menu wiring.
        _ = world;
    }
}
