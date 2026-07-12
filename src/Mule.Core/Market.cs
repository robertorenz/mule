using System.Collections.Generic;

namespace Mule.Core;

/// <summary>
/// Tracks current spot prices for each resource plus reference valuations used
/// for scoring. In the full game these move during the real-time auction; for now
/// they hold sensible starting values that the auction phase will drive.
/// </summary>
public sealed class Market
{
    private readonly Dictionary<Resource, int> _spot = new()
    {
        [Resource.Food] = 30,
        [Resource.Energy] = 25,
        [Resource.Smithore] = 50,
        [Resource.Crystite] = 100,
    };

    /// <summary>Store price of a new, unoutfitted MULE (rises as Smithore gets scarce).</summary>
    public int MulePrice { get; set; } = 100;

    /// <summary>Reference value of a plot of land for scoring.</summary>
    public int LandValue { get; set; } = 500;

    /// <summary>Reference value added by an installed MULE for scoring.</summary>
    public int MuleValue { get; set; } = 175;

    public int SpotPrice(Resource r) => _spot[r];
    public void SetSpotPrice(Resource r, int price) => _spot[r] = price;

    /// <summary>Clamp helper so auction moves never drive a price negative.</summary>
    public void AdjustSpotPrice(Resource r, int delta)
    {
        int next = _spot[r] + delta;
        _spot[r] = next < 1 ? 1 : next;
    }
}
