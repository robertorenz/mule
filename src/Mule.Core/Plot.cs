namespace Mule.Core;

/// <summary>
/// A single plot of land on the planet grid. A plot can be owned by a player and
/// may have a single MULE installed on it, outfitted to harvest one resource.
/// </summary>
public sealed class Plot
{
    public int X { get; }
    public int Y { get; }
    public Terrain Terrain { get; }

    /// <summary>Owning player id, or -1 if unowned.</summary>
    public int OwnerId { get; set; } = -1;

    /// <summary>What the installed MULE harvests, or None if no MULE is installed.</summary>
    public MuleOutfit Mule { get; set; } = MuleOutfit.None;

    public bool IsOwned => OwnerId >= 0;
    public bool HasMule => Mule != MuleOutfit.None;

    public Plot(int x, int y, Terrain terrain)
    {
        X = x;
        Y = y;
        Terrain = terrain;
    }
}
