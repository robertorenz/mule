namespace Mule.Core;

/// <summary>Outcome of an attempted player action, with a message to surface in the UI.</summary>
public readonly record struct ActionResult(bool Ok, string Message)
{
    public static ActionResult Fail(string message) => new(false, message);
    public static ActionResult Success(string message) => new(true, message);
}

/// <summary>
/// All the ways a player can change the world during their turn. Every rule and
/// validation lives here — the renderer, the AI, and any future network layer all
/// go through these methods so they can never disagree about what's legal.
/// </summary>
public static class ColonyActions
{
    /// <summary>Claim an unowned plot as a land grant.</summary>
    public static ActionResult ClaimPlot(GameState state, Player player, Plot plot)
    {
        if (plot.Terrain == Terrain.Town) return ActionResult.Fail("You can't claim the town.");
        if (plot.IsOwned)
            return ActionResult.Fail(plot.OwnerId == player.Id
                ? "You already own this plot."
                : "That plot belongs to another colonist.");

        plot.OwnerId = player.Id;
        return ActionResult.Success($"{player.Name} claimed a plot.");
    }

    /// <summary>Buy and outfit a MULE from the store. The colonist then leads it.</summary>
    public static ActionResult BuyMule(GameState state, Player player, MuleOutfit outfit)
    {
        if (outfit == MuleOutfit.None) return ActionResult.Fail("Choose what to outfit the MULE for.");
        if (player.IsLeadingMule) return ActionResult.Fail("You're already leading a MULE.");
        if (state.Store.MulesAvailable <= 0) return ActionResult.Fail("The store is out of MULEs.");

        int price = state.Store.MulePrice(outfit, state.Prices);
        if (player.Money < price) return ActionResult.Fail($"Not enough money (need ${price}).");

        player.Money -= price;
        state.Store.MulesAvailable--;
        player.CarriedMule = outfit;
        return ActionResult.Success($"Bought a {outfit} MULE for ${price}.");
    }

    /// <summary>Install the led MULE on a plot the player owns.</summary>
    public static ActionResult InstallMule(GameState state, Player player, Plot plot)
    {
        if (!player.IsLeadingMule) return ActionResult.Fail("You have no MULE to install.");
        if (plot.Terrain == Terrain.Town) return ActionResult.Fail("You can't install a MULE in town.");
        if (plot.OwnerId != player.Id) return ActionResult.Fail("You don't own this plot.");
        if (plot.HasMule) return ActionResult.Fail("This plot already has a MULE.");

        plot.Mule = player.CarriedMule;
        player.CarriedMule = MuleOutfit.None;
        return ActionResult.Success($"Installed a {plot.Mule} MULE.");
    }

    /// <summary>Buy Food or Energy units from the store at the current spot price.</summary>
    public static ActionResult BuyResource(GameState state, Player player, Resource resource, int quantity)
    {
        if (quantity <= 0) return ActionResult.Fail("Nothing to buy.");
        if (state.Store.Stock(resource) < quantity)
            return ActionResult.Fail($"The store is low on {resource}.");

        int price = state.Prices.SpotPrice(resource) * quantity;
        if (player.Money < price) return ActionResult.Fail($"Not enough money (need ${price}).");

        player.Money -= price;
        state.Store.RemoveStock(resource, quantity);
        player.AddStore(resource, quantity);
        return ActionResult.Success($"Bought {quantity} {resource} for ${price}.");
    }
}
