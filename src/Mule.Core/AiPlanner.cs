using System.Collections.Generic;

namespace Mule.Core;

public enum AiActionType { Claim, Buy, Install }

/// <summary>One step in an AI colonist's turn. For Buy the target is the town.</summary>
public readonly record struct AiAction(AiActionType Type, int X, int Y, MuleOutfit Outfit);

/// <summary>
/// Decides what an AI colonist does on its turn. Pure and deterministic: given the
/// same state it always plans the same actions, so it behaves identically on every
/// machine in a networked game and can be unit-tested without any rendering.
///
/// Strategy (deliberately simple for now): find the single most valuable place to
/// put a MULE this turn — the plot/resource pairing with the best expected payoff
/// the colonist can afford — then claim it if needed, buy the outfitted MULE, and
/// install it. Installing on land it already owns is preferred since it skips the
/// land cost.
/// </summary>
public static class AiPlanner
{
    public static List<AiAction> PlanTurn(GameState state, Player player)
    {
        var actions = new List<AiAction>();

        // No MULEs to be had means no useful move this turn — don't claim land we
        // can't equip.
        if (state.Store.MulesAvailable <= 0) return actions;

        Plot? bestPlot = null;
        Resource bestResource = default;
        float bestScore = float.NegativeInfinity;

        foreach (var plot in state.Map.AllPlots())
        {
            if (!plot.Terrain.IsBuildable() || plot.HasMule) continue;

            bool ownedByMe = plot.OwnerId == player.Id;
            bool unclaimed = !plot.IsOwned;
            if (!ownedByMe && !unclaimed) continue; // belongs to a rival

            foreach (Resource resource in System.Enum.GetValues<Resource>())
            {
                float yield = plot.Terrain.BaseYield(resource);
                if (yield <= 0) continue;

                MuleOutfit outfit = resource.ToOutfit();
                int cost = state.Store.MulePrice(outfit, state.Prices);
                if (player.Money < cost) continue;

                // Expected value of the goods produced, less a fraction of the MULE
                // cost, with a small edge for building on land we already hold.
                float value = yield * state.Prices.SpotPrice(resource) - cost * 0.3f;
                if (ownedByMe) value += 20f;

                if (value > bestScore)
                {
                    bestScore = value;
                    bestPlot = plot;
                    bestResource = resource;
                }
            }
        }

        if (bestPlot == null) return actions; // can't afford anything useful

        var chosenOutfit = bestResource.ToOutfit();
        if (!bestPlot.IsOwned)
            actions.Add(new AiAction(AiActionType.Claim, bestPlot.X, bestPlot.Y, MuleOutfit.None));

        var town = state.Map.Town();
        actions.Add(new AiAction(AiActionType.Buy, town.X, town.Y, chosenOutfit));
        actions.Add(new AiAction(AiActionType.Install, bestPlot.X, bestPlot.Y, chosenOutfit));

        return actions;
    }
}
