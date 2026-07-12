using System.Collections.Generic;

namespace Mule.Core;

/// <summary>
/// The complete, authoritative state of a single game. Everything needed to draw
/// the screen or resume play lives here — no state is hidden in the renderer.
/// This is the object a network layer will serialize and keep in sync.
/// </summary>
public sealed class GameState
{
    public int Seed { get; }
    public PlanetMap Map { get; }
    public Market Prices { get; } = new();
    public IReadOnlyList<Player> Players { get; }

    public GamePhase Phase { get; set; } = GamePhase.Setup;

    /// <summary>Current month (turn), 1-based. The colony survives a fixed number.</summary>
    public int Month { get; set; } = 1;

    public int TotalMonths { get; }

    /// <summary>Index into <see cref="Players"/> of whoever is currently acting.</summary>
    public int ActivePlayerIndex { get; set; }

    public Player ActivePlayer => Players[ActivePlayerIndex];

    public GameState(int seed, PlanetMap map, IReadOnlyList<Player> players, int totalMonths)
    {
        Seed = seed;
        Map = map;
        Players = players;
        TotalMonths = totalMonths;
    }

    public Player? PlayerById(int id)
    {
        foreach (var p in Players)
            if (p.Id == id) return p;
        return null;
    }
}
