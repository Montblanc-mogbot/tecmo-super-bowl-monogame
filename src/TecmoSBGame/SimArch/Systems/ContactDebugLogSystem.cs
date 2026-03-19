using System;
using Arch.Core;
using TecmoSBGame.SimArch.Events;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Debug logger for contact events.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/ContactDebugLogSystem.cs
/// </summary>
public sealed class ContactDebugLogSystem
{
    public bool Enabled { get; set; }

    public void Update(World world)
    {
        _ = world;
        if (!Enabled)
            return;

        foreach (var e in SimEventBus.Drain<TackleContactEvent>())
            Console.WriteLine($"[contact] tackle_contact def={e.DefenderId} carrier={e.BallCarrierId}");

        foreach (var e in SimEventBus.Drain<BlockContactEvent>())
            Console.WriteLine($"[contact] block_contact blk={e.BlockerId} def={e.DefenderId}");
    }
}
