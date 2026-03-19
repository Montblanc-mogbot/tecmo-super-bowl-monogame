using System;

namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Ported from: ArchiveMge/Components/Menu/MenuItemComponent.cs
/// </summary>
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

/// <summary>
/// Plain C# menu "component" (managed) used by the UI/navigation layer.
///
/// NOTE: This is not performance critical and may remain managed.
/// Ported from: ArchiveMge/Components/Menu/MenuItemComponent.cs
/// </summary>
public sealed class MenuItem
{
    public MenuItem(MenuItemType type, string label, Action<MenuItemType>? onSelect = null)
    {
        Type = type;
        Label = label ?? throw new ArgumentNullException(nameof(label));
        OnSelect = onSelect;
    }

    public string Label { get; }
    public bool IsSelected { get; internal set; }
    public MenuItemType Type { get; }

    /// <summary>Invoked when the item is selected.</summary>
    public Action<MenuItemType>? OnSelect { get; set; }
}
