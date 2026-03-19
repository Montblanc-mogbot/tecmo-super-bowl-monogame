using System;
using System.Collections.Generic;

namespace TecmoSBGame.SimArch.Routes;

/// <summary>
/// Holds managed route plans for the current Sim instance.
/// Components reference routes by integer index.
/// </summary>
public sealed class RouteRegistry
{
    private readonly List<RoutePlan> _routes = new();

    public int Add(RoutePlan plan)
    {
        if (plan.Nodes is null || plan.Nodes.Length == 0)
            throw new ArgumentException("Route plan must have nodes", nameof(plan));

        var idx = _routes.Count;
        _routes.Add(plan);
        return idx;
    }

    public RoutePlan Get(int index) => _routes[index];

    public void Clear() => _routes.Clear();
}
