using System.Collections.Generic;

namespace Mule.Core;

/// <summary>
/// A colonist — human or AI. Holds money, a store of each resource, and any
/// MULEs currently in inventory (not yet installed on a plot).
/// </summary>
public sealed class Player
{
    public int Id { get; }
    public string Name { get; set; }
    public bool IsAI { get; set; }

    /// <summary>Display color as packed RGBA (0xRRGGBBAA). Kept engine-agnostic.</summary>
    public uint Color { get; set; }

    public int Money { get; set; }

    /// <summary>Uninstalled MULEs the player is carrying.</summary>
    public int MulesInInventory { get; set; }

    private readonly Dictionary<Resource, int> _stores = new()
    {
        [Resource.Food] = 0,
        [Resource.Energy] = 0,
        [Resource.Smithore] = 0,
        [Resource.Crystite] = 0,
    };

    public Player(int id, string name, uint color, bool isAI)
    {
        Id = id;
        Name = name;
        Color = color;
        IsAI = isAI;
    }

    public int Store(Resource r) => _stores[r];
    public void AddStore(Resource r, int amount) => _stores[r] += amount;
    public void SetStore(Resource r, int amount) => _stores[r] = amount;

    /// <summary>Total wealth used for scoring: cash plus land and goods valuations.</summary>
    public int Score(GameState state)
    {
        int score = Money;
        foreach (var plot in state.Map.AllPlots())
            if (plot.OwnerId == Id)
                score += state.Prices.LandValue + (plot.HasMule ? state.Prices.MuleValue : 0);

        foreach (Resource r in System.Enum.GetValues<Resource>())
            score += _stores[r] * state.Prices.SpotPrice(r);

        return score;
    }
}
