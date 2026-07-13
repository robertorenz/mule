using System.Collections.Generic;

namespace Mule.Core;

/// <summary>
/// The town store. Sells MULEs (a limited supply tied to Smithore in the full
/// game) and stocks Food and Energy for colonists to buy. Prices for the four
/// resources live on <see cref="Market"/>; the store adds an outfitting surcharge
/// on top of the base MULE price.
/// </summary>
public sealed class Store
{
    /// <summary>MULEs the store can still sell this game. Refilled from Smithore later.</summary>
    public int MulesAvailable { get; set; } = 14;

    private readonly Dictionary<Resource, int> _stock = new()
    {
        [Resource.Food] = 8,
        [Resource.Energy] = 8,
        [Resource.Smithore] = 0,
        [Resource.Crystite] = 0,
    };

    public int Stock(Resource r) => _stock[r];
    public void AddStock(Resource r, int amount) => _stock[r] += amount;
    public void RemoveStock(Resource r, int amount) => _stock[r] -= amount;

    private int _smithoreBuffer;

    /// <summary>Partial Smithore accumulated toward the next MULE. Exposed for snapshots.</summary>
    public int SmithoreBuffer { get => _smithoreBuffer; set => _smithoreBuffer = value; }

    /// <summary>
    /// Feed Smithore the store bought at auction into MULE manufacturing. Every two
    /// units of Smithore yields one new MULE, restocking the store's supply.
    /// Returns how many MULEs were minted.
    /// </summary>
    public int RefineSmithore(int units)
    {
        _smithoreBuffer += units;
        int made = _smithoreBuffer / 2;
        _smithoreBuffer %= 2;
        MulesAvailable += made;
        return made;
    }

    /// <summary>Cost of outfitting a fresh MULE for a given resource.</summary>
    public int OutfitSurcharge(MuleOutfit outfit) => outfit switch
    {
        MuleOutfit.Food => 25,
        MuleOutfit.Energy => 25,
        MuleOutfit.Smithore => 50,
        MuleOutfit.Crystite => 100,
        _ => 0
    };

    /// <summary>Total price to buy and outfit a MULE for the given resource.</summary>
    public int MulePrice(MuleOutfit outfit, Market market) => market.MulePrice + OutfitSurcharge(outfit);
}
