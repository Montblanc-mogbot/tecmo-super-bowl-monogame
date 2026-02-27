using System;
using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Events;

namespace TecmoSBGame.Systems;

/// <summary>
/// Headless-friendly debug logging for contact events.
/// Uses Read() (not Drain) so it does not interfere with other consumers.
/// </summary>
public sealed class ContactDebugLogSystem : UpdateSystem
{
    private readonly GameEvents _events;

    public ContactDebugLogSystem(GameEvents events)
    {
        _events = events;
    }

    public override void Update(GameTime gameTime)
    {
        var tackles = _events.Read<TackleContactEvent>();
        for (var i = 0; i < tackles.Count; i++)
        {
            var t = tackles[i];
            Console.WriteLine($"[contact] tackle defender={t.DefenderId} carrier={t.BallCarrierId} pos=({t.Position.X:0.0},{t.Position.Y:0.0})");
        }

        var blocks = _events.Read<BlockContactEvent>();
        for (var i = 0; i < blocks.Count; i++)
        {
            var b = blocks[i];
            Console.WriteLine($"[contact] block blocker={b.BlockerId} defender={b.DefenderId} pos=({b.Position.X:0.0},{b.Position.Y:0.0})");
        }
    }
}
