using System;
using Gum.Forms;
using Gum.Forms.Controls;
using Microsoft.Xna.Framework;
using MonoGameGum;

namespace TecmoSBGame.UI.Playcall;

/// <summary>
/// Code-only Gum.Forms screen for Playcall.
///
/// Minimal scaffold:
/// - Left: formations list
/// - Right: plays list
/// - Bottom: confirm button
///
/// Asset-backed visuals can replace this later once the Gum project is checked in.
/// </summary>
public sealed class PlaycallScreen : ScreenBase
{
    public ListBox FormationList { get; }
    public ListBox PlayList { get; }
    public Button ConfirmButton { get; }

    public event Action<int, int>? Confirmed;

    public PlaycallScreen()
    {
        // Ensure we're in a Forms-ready Gum context.
        if (!GumService.Default.IsInitialized)
            throw new InvalidOperationException("GumService must be initialized before creating PlaycallScreen");

        // Root container
        Visual.Width = GumService.Default.CanvasWidth;
        Visual.Height = GumService.Default.CanvasHeight;


        // Formations list
        FormationList = new ListBox();
        FormationList.Visual.Width = 200;
        FormationList.Visual.Height = Math.Max(100, Visual.Height - 80);
        FormationList.Visual.X = 16;
        FormationList.Visual.Y = 16;

        // Plays list
        PlayList = new ListBox();
        PlayList.Visual.Width = 280;
        PlayList.Visual.Height = FormationList.Visual.Height;
        PlayList.Visual.X = FormationList.Visual.X + FormationList.Visual.Width + 16;
        PlayList.Visual.Y = 16;

        // Confirm
        ConfirmButton = new Button();
        ConfirmButton.Text = "Confirm";
        ConfirmButton.Visual.Width = 120;
        ConfirmButton.Visual.Height = 36;
        ConfirmButton.Visual.X = PlayList.Visual.X + PlayList.Visual.Width - ConfirmButton.Visual.Width;
        ConfirmButton.Visual.Y = FormationList.Visual.Y + FormationList.Visual.Height + 12;
        ConfirmButton.Click += (_, _) =>
        {
            var formationIndex = FormationList.SelectedIndex;
            var playIndex = PlayList.SelectedIndex;
            if (formationIndex < 0 || playIndex < 0)
                return;
            Confirmed?.Invoke(formationIndex, playIndex);
        };

        // Attach controls directly under the screen root.
        Visual.Children.Add(FormationList.Visual);
        Visual.Children.Add(PlayList.Visual);
        Visual.Children.Add(ConfirmButton.Visual);
    }
}
