using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Minimal input reader during PostPlay: publish an intent to continue.
/// This is kept deterministic by running inside the fixed-step ECS update.
/// </summary>
public sealed class PostPlayContinueInputSystem : UpdateSystem
{
    private readonly GameEvents? _events;
    private readonly PlayState _play;

    private bool _prevContinueDown;

    public PostPlayContinueInputSystem(GameEvents? events, PlayState playState)
    {
        _events = events;
        _play = playState ?? throw new ArgumentNullException(nameof(playState));
    }

    public override void Update(GameTime gameTime)
    {
        if (_events is null)
            return;

        if (_play.Phase != PlayPhase.PostPlay)
        {
            _prevContinueDown = false;
            return;
        }

        // Auto-dismiss for turnovers/scores.
        if (_play.AutoDismissPostPlay)
        {
            _events.Publish(new PostPlayContinueRequestedEvent(_play.PlayId));
            return;
        }

        var kb = Keyboard.GetState();
        var gp = GamePad.GetState(PlayerIndex.One);

        var continueDown = kb.IsKeyDown(Keys.Enter) || kb.IsKeyDown(Keys.Space) || kb.IsKeyDown(Keys.Escape) ||
                           gp.Buttons.Start == ButtonState.Pressed || gp.Buttons.A == ButtonState.Pressed;

        var pressed = continueDown && !_prevContinueDown;
        _prevContinueDown = continueDown;

        if (pressed)
            _events.Publish(new PostPlayContinueRequestedEvent(_play.PlayId));
    }
}
