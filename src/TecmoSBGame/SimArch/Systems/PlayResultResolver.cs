using System;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.State;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Computes play result (yards gained + end spot) from world coordinates.
///
/// For now we only resolve tackle-end plays.
/// </summary>
public sealed class PlayResultResolver
{
    private readonly MatchState _match;
    private readonly PlayState _play;

    public PlayResultResolver(MatchState match, PlayState play)
    {
        _match = match ?? throw new ArgumentNullException(nameof(match));
        _play = play ?? throw new ArgumentNullException(nameof(play));
    }

    public void ResolveOnTackle(World world, int ballEntityId)
    {
        if (!TryGetBallPosition(world, ballEntityId, out var ballPos))
            return;

        var endAbs = FieldMapping.BallToAbsoluteYard(ballPos);
        _play.EndAbsoluteYard = endAbs;

        var gained = endAbs - _play.StartAbsoluteYard;
        if (_match.OffenseDirection == OffenseDirection.RightToLeft)
            gained = -gained;

        _play.Result = new PlayResult(
            YardsGained: gained,
            Turnover: false,
            Touchdown: false,
            Safety: false);
    }

    private static bool TryGetBallPosition(World world, int ballEntityId, out Vector2 pos)
    {
        pos = default;
        var found = false;
        var local = Vector2.Zero;

        var q = new QueryDescription().WithAll<Ball, Position>();
        world.Query(in q, (Entity e, ref Ball _, ref Position p) =>
        {
            if (found)
                return;
            if (e.Id != ballEntityId)
                return;

            local = p.Value;
            found = true;
        });

        if (!found)
            return false;

        pos = local;
        return true;
    }
}
