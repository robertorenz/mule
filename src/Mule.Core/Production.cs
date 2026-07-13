using System;
using System.Collections.Generic;

namespace Mule.Core;

/// <summary>A single plot's contribution during production, for reporting to the UI.</summary>
public readonly record struct ProductionResult(int PlayerId, Resource Resource, int Amount, int X, int Y);

/// <summary>Outcome of the production phase: what was produced, and what stalled for lack of power.</summary>
public readonly record struct ProductionReport(IReadOnlyList<ProductionResult> Yields, int UnpoweredMules);

/// <summary>
/// Resolves the production phase. Every installed MULE harvests its resource based
/// on the plot's terrain — but a harvesting MULE must be powered by one unit of the
/// owner's Energy. Energy MULEs are exempt (they generate power). When a colonist
/// can't power all their MULEs, the unpowered ones produce nothing this month, which
/// is what makes the Energy auction matter.
///
/// Deterministic: plots resolve in a fixed order so a networked game agrees.
/// </summary>
public static class Production
{
    public static ProductionReport Resolve(GameState state)
    {
        var results = new List<ProductionResult>();
        int unpowered = 0;

        // Pass 1: Energy MULEs generate power first, so what a colonist makes this
        // month is available to run their other MULEs this same month. Leftover
        // Energy stays in the store to sell or carry over.
        foreach (var plot in state.Map.AllPlots())
        {
            if (!plot.HasMule || !plot.IsOwned || plot.Mule != MuleOutfit.Energy) continue;
            var owner = state.PlayerById(plot.OwnerId);
            if (owner == null) continue;

            int amount = (int)MathF.Round(plot.Terrain.BaseYield(Resource.Energy));
            if (amount <= 0) continue;

            owner.AddStore(Resource.Energy, amount);
            results.Add(new ProductionResult(owner.Id, Resource.Energy, amount, plot.X, plot.Y));
        }

        // Pass 2: every other MULE draws one unit of the owner's Energy to run.
        // With none left, it sits idle and produces nothing this month.
        foreach (var plot in state.Map.AllPlots())
        {
            if (!plot.HasMule || !plot.IsOwned || plot.Mule == MuleOutfit.Energy) continue;
            var owner = state.PlayerById(plot.OwnerId);
            if (owner == null) continue;

            if (owner.Store(Resource.Energy) <= 0) { unpowered++; continue; }
            owner.AddStore(Resource.Energy, -1);

            Resource resource = plot.Mule.ToResource();
            int amount = (int)MathF.Round(plot.Terrain.BaseYield(resource));
            if (amount <= 0) continue;

            owner.AddStore(resource, amount);
            results.Add(new ProductionResult(owner.Id, resource, amount, plot.X, plot.Y));
        }

        return new ProductionReport(results, unpowered);
    }
}
