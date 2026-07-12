namespace Mule.Core;

/// <summary>
/// Terrain type of a map plot. Terrain biases how well a plot produces each
/// resource: rivers grow the most Food, mountains hold the most Smithore/Crystite,
/// and plains are the most reliable for Energy.
/// </summary>
public enum Terrain
{
    Plain,
    River,
    Mountain1,
    Mountain2,
    Mountain3,
    Town
}

public static class TerrainExtensions
{
    /// <summary>
    /// Base production multiplier for a given resource on this terrain, before
    /// player skill, adjacency and random events are applied.
    /// </summary>
    public static float BaseYield(this Terrain t, Resource r) => (t, r) switch
    {
        (Terrain.River, Resource.Food) => 4f,
        (Terrain.Plain, Resource.Food) => 2f,
        (Terrain.Mountain1, Resource.Food) => 1f,
        (Terrain.Mountain2, Resource.Food) => 1f,
        (Terrain.Mountain3, Resource.Food) => 1f,

        (Terrain.Plain, Resource.Energy) => 3f,
        (Terrain.River, Resource.Energy) => 2f,
        (Terrain.Mountain1, Resource.Energy) => 1f,
        (Terrain.Mountain2, Resource.Energy) => 1f,
        (Terrain.Mountain3, Resource.Energy) => 1f,

        (Terrain.Mountain1, Resource.Smithore) => 1f,
        (Terrain.Mountain2, Resource.Smithore) => 2f,
        (Terrain.Mountain3, Resource.Smithore) => 3f,
        (Terrain.Plain, Resource.Smithore) => 0f,
        (Terrain.River, Resource.Smithore) => 0f,

        // Crystite only comes from mountains, and only meaningfully at higher levels.
        (Terrain.Mountain1, Resource.Crystite) => 1f,
        (Terrain.Mountain2, Resource.Crystite) => 1f,
        (Terrain.Mountain3, Resource.Crystite) => 1f,

        _ => 0f
    };

    public static bool IsBuildable(this Terrain t) => t != Terrain.Town;
}
