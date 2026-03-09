using System;
using System.Collections.Generic;
using TecmoSBGame.Components.Menu;
using TecmoSBGame.Input;

namespace TecmoSBGame.Systems.Menu;

/// <summary>
/// Menu navigation helper (non-ECS). Owns a selection index and responds to InputManager menu events.
/// </summary>
public sealed class MenuNavigationSystem : IDisposable
{
    private readonly InputManager _input;
    private readonly List<MenuItemComponent> _items = new();
    private int _selectedIndex;

    public MenuNavigationSystem(InputManager input)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));

        _input.OnMenuUp += NavigateUp;
        _input.OnMenuDown += NavigateDown;
        _input.OnMenuSelect += Select;
    }

    public bool Enabled { get; set; } = true;

    public IReadOnlyList<MenuItemComponent> Items => _items;

    public int SelectedIndex => _selectedIndex;

    public MenuItemComponent? SelectedItem => _items.Count == 0 ? null : _items[_selectedIndex];

    public void SetItems(IEnumerable<MenuItemComponent> items, int selectedIndex = 0)
    {
        _items.Clear();
        _items.AddRange(items);

        _selectedIndex = _items.Count == 0 ? 0 : Clamp(selectedIndex, 0, _items.Count - 1);
        ApplySelectionFlags();
    }

    public void NavigateUp()
    {
        if (!Enabled || _items.Count == 0)
            return;

        _selectedIndex--;
        if (_selectedIndex < 0)
            _selectedIndex = _items.Count - 1;

        ApplySelectionFlags();
        // TODO: play navigation sfx.
    }

    public void NavigateDown()
    {
        if (!Enabled || _items.Count == 0)
            return;

        _selectedIndex++;
        if (_selectedIndex >= _items.Count)
            _selectedIndex = 0;

        ApplySelectionFlags();
        // TODO: play navigation sfx.
    }

    public void Select()
    {
        if (!Enabled)
            return;

        var item = SelectedItem;
        if (item is null)
            return;

        // TODO: play select sfx.
        item.OnSelect?.Invoke(item.Type);
    }

    private void ApplySelectionFlags()
    {
        for (int i = 0; i < _items.Count; i++)
            _items[i].IsSelected = i == _selectedIndex;
    }

    private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);

    public void Dispose()
    {
        _input.OnMenuUp -= NavigateUp;
        _input.OnMenuDown -= NavigateDown;
        _input.OnMenuSelect -= Select;
    }
}
