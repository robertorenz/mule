namespace Mule.Core;

/// <summary>
/// The four tradeable commodities in M.U.L.E.
/// Food buys player time each turn, Energy powers production,
/// Smithore is refined into new MULEs, and Crystite is a pure export good.
/// </summary>
public enum Resource
{
    Food,
    Energy,
    Smithore,
    Crystite
}

/// <summary>
/// What a MULE has been outfitted to harvest. Unoutfitted MULEs produce nothing.
/// </summary>
public enum MuleOutfit
{
    None,
    Food,
    Energy,
    Smithore,
    Crystite
}

public static class ResourceExtensions
{
    public static Resource ToResource(this MuleOutfit outfit) => outfit switch
    {
        MuleOutfit.Food => Resource.Food,
        MuleOutfit.Energy => Resource.Energy,
        MuleOutfit.Smithore => Resource.Smithore,
        MuleOutfit.Crystite => Resource.Crystite,
        _ => throw new System.InvalidOperationException("Unoutfitted MULE has no resource.")
    };

    public static string DisplayName(this Resource r) => r.ToString();
}
