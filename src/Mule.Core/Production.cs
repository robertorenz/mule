using System;
using System.Collections.Generic;

namespace Mule.Core;

/// <summary>A single plot's contribution during production, for reporting to the UI.</summary>
public readonly record struct ProductionResult(int PlayerId, Resource Resource, int Amount, int X, int Y);

/// <summary>
/// Resolves the production phase: every installed MULE harvests its resource based
/// on the plot's terrain, crediting the owner's stores. Kept deterministic so a
/// networked game produces identical results everywhere.
/// </summary>
public static class Production
{
    public static IReadOnlyList<ProductionResult> Resolve(GameState state)
    {
        var results = new List<ProductionResult>();

        foreach (var plot in state.Map.AllPlots())
        {
            if (!plot.HasMule || !plot.IsOwned) continue;
            var owner = state.PlayerById(plot.OwnerId);
            if (owner == null) continue;

            Resource resource = plot.Mule.ToResource();
            int amount = (int)MathF.Round(plot.Terrain.BaseYield(resource));
            if (amount <= 0) continue;

            owner.AddStore(resource, amount);
            results.Add(new ProductionResult(owner.Id, resource, amount, plot.X, plot.Y));
        }

        return results;
    }
}
