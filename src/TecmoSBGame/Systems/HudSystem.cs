using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Flow;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Pulls HUD-relevant values from <see cref="MatchState"/> / <see cref="PlayState"/> and writes them
/// into <see cref="HudComponent"/> instances.
///
/// This system does not render. Renderers should be invoked from <c>MainGame.Draw</c>.
/// </summary>
public sealed class HudSystem : EntityUpdateSystem
{
    private readonly MatchState _match;
    private readonly PlayState _play;
    private readonly GameFlowController _flow;

    private ComponentMapper<HudComponent> _hud = null!;

    public HudSystem(MatchState matchState, PlayState playState, GameFlowController flow)
        : base(Aspect.All(typeof(HudComponent)))
    {
        _match = matchState ?? throw new ArgumentNullException(nameof(matchState));
        _play = playState ?? throw new ArgumentNullException(nameof(playState));
        _flow = flow ?? throw new ArgumentNullException(nameof(flow));
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _hud = mapperService.GetMapper<HudComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        // HUD is only relevant during field gameplay slices.
        var showHud = _flow.State is GameFlowState.Kickoff or GameFlowState.OnField or GameFlowState.PostPlay;

        foreach (var entityId in ActiveEntities)
        {
            var h = _hud.Get(entityId);
            h.Visible = showHud;

            if (!showHud)
                continue;

            switch (h.ElementType)
            {
                case HudElementType.Scoreboard:
                    h.Int0 = _match.Team0Score;
                    h.Int1 = _match.Team1Score;
                    h.Int2 = _match.GameClockSeconds;
                    h.Text0 = $"Q{_match.Quarter}";
                    h.Text1 = MatchState.FormatClock(_match.GameClockSeconds);
                    break;

                case HudElementType.DownDistance:
                    h.Int0 = _match.Down;
                    h.Int1 = _match.YardsToGo;
                    h.Text0 = _match.FormatDownDistance();
                    h.Text1 = _match.BallSpot.ToString();
                    break;

                case HudElementType.PlayClock:
                    // No play clock model yet; keep as placeholder.
                    h.Text0 = "";
                    break;

                case HudElementType.PossessionIndicator:
                    h.Int0 = _match.PossessionTeam;
                    h.Text0 = $"T{_match.PossessionTeam}";
                    break;
            }
        }

        _ = _play; // reserved for future per-play HUD bindings (play clock, ball carrier, etc.)
    }
}
