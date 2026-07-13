using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Mule.Core;

/// <summary>
/// A serializable, flat snapshot of an entire <see cref="GameState"/>. This is what
/// the authoritative host sends to clients so everyone shows the same colony. The
/// map is regenerated from the seed on restore (it's deterministic), so only the
/// dynamic parts — ownership, MULEs, money, stores, prices, phase — travel the wire.
/// </summary>
public sealed class GameSnapshot
{
    public int Version { get; set; } = 1;
    public int Seed { get; set; }
    public int Month { get; set; }
    public int TotalMonths { get; set; }
    public int Phase { get; set; }
    public int ActivePlayerIndex { get; set; }

    public int[] SpotPrices { get; set; } = new int[4];
    public int MulePrice { get; set; }
    public int LandValue { get; set; }
    public int MuleValue { get; set; }

    public int StoreMules { get; set; }
    public int[] StoreStock { get; set; } = new int[4];
    public int SmithoreBuffer { get; set; }

    public List<PlayerSnap> Players { get; set; } = new();
    public List<PlotSnap> Plots { get; set; } = new();

    public sealed class PlayerSnap
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool IsAI { get; set; }
        public uint Color { get; set; }
        public int Money { get; set; }
        public int CarriedMule { get; set; }
        public float TimeFactor { get; set; }
        public int[] Stores { get; set; } = new int[4];
    }

    public sealed class PlotSnap
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int OwnerId { get; set; }
        public int Mule { get; set; }
    }

    public static GameSnapshot Capture(GameState s)
    {
        var snap = new GameSnapshot
        {
            Seed = s.Seed,
            Month = s.Month,
            TotalMonths = s.TotalMonths,
            Phase = (int)s.Phase,
            ActivePlayerIndex = s.ActivePlayerIndex,
            MulePrice = s.Prices.MulePrice,
            LandValue = s.Prices.LandValue,
            MuleValue = s.Prices.MuleValue,
            StoreMules = s.Store.MulesAvailable,
            SmithoreBuffer = s.Store.SmithoreBuffer,
        };

        foreach (Resource r in Enum.GetValues<Resource>())
        {
            snap.SpotPrices[(int)r] = s.Prices.SpotPrice(r);
            snap.StoreStock[(int)r] = s.Store.Stock(r);
        }

        foreach (var p in s.Players)
        {
            var ps = new PlayerSnap
            {
                Id = p.Id,
                Name = p.Name,
                IsAI = p.IsAI,
                Color = p.Color,
                Money = p.Money,
                CarriedMule = (int)p.CarriedMule,
                TimeFactor = p.TimeFactor,
            };
            foreach (Resource r in Enum.GetValues<Resource>())
                ps.Stores[(int)r] = p.Store(r);
            snap.Players.Add(ps);
        }

        // Only plots that differ from a fresh map need sending.
        foreach (var plot in s.Map.AllPlots())
            if (plot.IsOwned || plot.HasMule)
                snap.Plots.Add(new PlotSnap { X = plot.X, Y = plot.Y, OwnerId = plot.OwnerId, Mule = (int)plot.Mule });

        return snap;
    }

    public static GameState Restore(GameSnapshot snap)
    {
        var map = PlanetMap.Generate(snap.Seed);

        var players = new List<Player>();
        foreach (var ps in snap.Players)
        {
            var p = new Player(ps.Id, ps.Name, ps.Color, ps.IsAI)
            {
                Money = ps.Money,
                CarriedMule = (MuleOutfit)ps.CarriedMule,
                TimeFactor = ps.TimeFactor,
            };
            foreach (Resource r in Enum.GetValues<Resource>())
                p.SetStore(r, ps.Stores[(int)r]);
            players.Add(p);
        }

        var state = new GameState(snap.Seed, map, players, snap.TotalMonths)
        {
            Phase = (GamePhase)snap.Phase,
            Month = snap.Month,
            ActivePlayerIndex = snap.ActivePlayerIndex,
        };

        foreach (Resource r in Enum.GetValues<Resource>())
        {
            state.Prices.SetSpotPrice(r, snap.SpotPrices[(int)r]);
            state.Store.RemoveStock(r, state.Store.Stock(r));  // zero the default stock
            state.Store.AddStock(r, snap.StoreStock[(int)r]);
        }
        state.Prices.MulePrice = snap.MulePrice;
        state.Prices.LandValue = snap.LandValue;
        state.Prices.MuleValue = snap.MuleValue;
        state.Store.MulesAvailable = snap.StoreMules;
        state.Store.SmithoreBuffer = snap.SmithoreBuffer;

        foreach (var plotSnap in snap.Plots)
        {
            var plot = map.At(plotSnap.X, plotSnap.Y);
            plot.OwnerId = plotSnap.OwnerId;
            plot.Mule = (MuleOutfit)plotSnap.Mule;
        }

        return state;
    }

    public string ToJson() => JsonSerializer.Serialize(this);
    public static GameSnapshot FromJson(string json) => JsonSerializer.Deserialize<GameSnapshot>(json)!;
}
