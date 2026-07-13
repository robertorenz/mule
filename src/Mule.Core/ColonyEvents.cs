using System;
using System.Collections.Generic;

namespace Mule.Core;

/// <summary>A resolved monthly event, described for the news bulletin.</summary>
public readonly record struct EventReport(bool IsGood, string Headline, string Detail, int AffectedPlayerId);

/// <summary>
/// Random monthly colony events — the news bulletin that opens each month after the
/// first. Deterministic: the event is drawn from an RNG seeded by the game seed and
/// the month, so every machine in a networked game sees the same news.
///
/// In the spirit of the original, luck leans toward balance: good fortune tends to
/// find the colonist in last place, while trouble tends to visit the leader.
/// </summary>
public static class ColonyEvents
{
    public static EventReport? Resolve(GameState state)
    {
        if (state.Month <= 1) return null; // a calm first month to settle in

        var rng = new Random(state.Seed * 1000 + state.Month);
        return rng.Next(8) switch
        {
            0 => Windfall(state, rng),
            1 => Package(state),
            2 => LandGrant(state, rng),
            3 => Meteor(state, rng),
            4 => Pest(state, rng),
            5 => Sunspot(state),
            6 => Pirates(state, rng),
            _ => Malfunction(state, rng),
        };
    }

    private static Player Lowest(GameState s)
    {
        Player low = s.Players[0];
        foreach (var p in s.Players)
            if (p.Score(s) < low.Score(s)) low = p;
        return low;
    }

    private static Player Highest(GameState s)
    {
        Player high = s.Players[0];
        foreach (var p in s.Players)
            if (p.Score(s) > high.Score(s)) high = p;
        return high;
    }

    // ---- Good fortune (favors the trailing colonist) -----------------------

    private static EventReport Windfall(GameState s, Random rng)
    {
        var p = Lowest(s);
        int amount = 50 + rng.Next(6) * 25; // 50..175
        p.Money += amount;
        return new EventReport(true, "Lucky Strike",
            $"{p.Name} unearths a buried cache and gains ${amount}.", p.Id);
    }

    private static EventReport Package(GameState s)
    {
        var p = Lowest(s);
        p.AddStore(Resource.Food, 3);
        p.AddStore(Resource.Energy, 2);
        return new EventReport(true, "Supply Ship",
            $"A shipment from home brings {p.Name} 3 Food and 2 Energy.", p.Id);
    }

    private static EventReport LandGrant(GameState s, Random rng)
    {
        foreach (var plot in s.Map.AllPlots())
        {
            if (plot.Terrain.IsBuildable() && !plot.IsOwned)
            {
                var p = Lowest(s);
                plot.OwnerId = p.Id;
                return new EventReport(true, "Homestead Grant",
                    $"The colony deeds {p.Name} an unclaimed plot of land.", p.Id);
            }
        }
        return Windfall(s, rng); // nothing left to grant
    }

    private static EventReport Meteor(GameState s, Random rng)
    {
        var p = Lowest(s);
        int crystite = 4 + rng.Next(5); // 4..8
        p.AddStore(Resource.Crystite, crystite);
        return new EventReport(true, "Meteorite Shower",
            $"A meteorite salts {p.Name}'s land with {crystite} Crystite.", p.Id);
    }

    // ---- Misfortune (favors troubling the leader) --------------------------

    private static EventReport Pest(GameState s, Random rng)
    {
        var p = Highest(s);
        int loss = Math.Min(p.Store(Resource.Food), 2 + rng.Next(3));
        p.AddStore(Resource.Food, -loss);
        return new EventReport(false, "Pest Infestation",
            loss > 0 ? $"Pests devour {loss} of {p.Name}'s Food."
                     : $"Pests raid {p.Name}'s larder but find it bare.", p.Id);
    }

    private static EventReport Sunspot(GameState s)
    {
        foreach (var p in s.Players)
            p.AddStore(Resource.Energy, -Math.Min(p.Store(Resource.Energy), 1));
        return new EventReport(false, "Sunspot Activity",
            "A solar storm drains 1 Energy from every colonist.", -1);
    }

    private static EventReport Pirates(GameState s, Random rng)
    {
        var p = Highest(s);
        int loss = Math.Min(p.Money, 50 + rng.Next(6) * 25);
        p.Money -= loss;
        return new EventReport(false, "Space Pirates",
            $"Pirates raid {p.Name} and make off with ${loss}.", p.Id);
    }

    private static EventReport Malfunction(GameState s, Random rng)
    {
        var withMules = new List<Plot>();
        foreach (var plot in s.Map.AllPlots())
            if (plot.HasMule && plot.IsOwned) withMules.Add(plot);

        if (withMules.Count == 0) return Sunspot(s); // nothing to break yet

        var hit = withMules[rng.Next(withMules.Count)];
        var owner = s.PlayerById(hit.OwnerId);
        hit.Mule = MuleOutfit.None;
        return new EventReport(false, "MULE Malfunction",
            $"One of {owner?.Name}'s MULEs breaks down and wanders off.", hit.OwnerId);
    }
}
