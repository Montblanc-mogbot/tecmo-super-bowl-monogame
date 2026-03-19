using System;
using Gum.Forms;
using Microsoft.Xna.Framework;
using MonoGameGum;

namespace TecmoSBGame.UI.Gum;

/// <summary>
/// Central bootstrap point for Gum (MonoGameGum) integration.
///
/// Goals:
/// - One place to initialize GumService + Forms defaults.
/// - One place to create and assign the root screen.
///
/// NOTE: We currently support running without a Gum project loaded (code-only UI).
/// When we add a Gum project under Content/UI/Gum, pass its gumx path to Initialize.
/// </summary>
public static class GumBootstrap
{
    public static GumService Service => GumService.Default;

    /// <summary>
    /// Initializes Gum. Call once early during game startup.
    /// </summary>
    /// <param name="game">The MonoGame Game instance.</param>
    /// <param name="gumProjectFile">
    /// Optional Gum project file to load (e.g. "Content/UI/Gum/TecmoUi.gumx").
    /// If null/empty, Gum runs in code-only mode.
    /// </param>
    public static void Initialize(Game game, string? gumProjectFile = null)
    {
        if (Service.IsInitialized)
            return;

        Service.Initialize(game, string.IsNullOrWhiteSpace(gumProjectFile) ? null : gumProjectFile);

        // Enable Gum.Forms defaults (cursor, keyboard, gamepads, default visuals).
        FormsUtilities.InitializeDefaults(game, Service.SystemManagers, DefaultVisualsVersion.Newest);
    }

    /// <summary>
    /// Creates a simple root screen and assigns it to GumService.Root.
    /// </summary>
    public static ScreenBase LoadRootScreen()
    {
        if (!Service.IsInitialized)
            throw new InvalidOperationException("GumBootstrap.Initialize must be called before LoadRootScreen");

        var screen = new ScreenBase();

        // The GumService root is a get-only container; attach our screen as a child.
        // (GumService maintains an internal root even in code-only mode.)
        Service.Root.Children.Clear();
        Service.Root.Children.Add(screen.Visual);

        return screen;
    }
}
