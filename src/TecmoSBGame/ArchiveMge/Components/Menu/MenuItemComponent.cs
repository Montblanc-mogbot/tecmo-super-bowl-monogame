using System;

namespace TecmoSBGame.Components.Menu;

/// <summary>
/// Plain C# menu "component" (non-ECS) used by the UI/navigation layer.
/// </summary>
public sealed class MenuItemComponent
{
    public MenuItemComponent(MenuItemType type, string label, Action<MenuItemType>? onSelect = null)
    {
        Type = type;
        Label = label ?? throw new ArgumentNullException(nameof(label));
        OnSelect = onSelect;
    }

    public string Label { get; }
    public bool IsSelected { get; internal set; }
    public MenuItemType Type { get; }

    /// <summary>
    /// Invoked when the item is selected.
    /// </summary>
    public Action<MenuItemType>? OnSelect { get; set; }
}

public enum MenuItemType
{
    None = 0,

    // Main Menu
    Preseason = 1,
    Season = 2,
    ProBowl = 3,
    Options = 4,
    Data = 5,
}
