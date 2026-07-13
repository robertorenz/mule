using System;
using System.Collections.Generic;

namespace Mule.Core;

public enum AuctionRole { Buyer, Seller }

/// <summary>
/// One participant in a resource auction: a player, or the store (PlayerId -1).
/// Its <see cref="Price"/> is the bid (buyer) or ask (seller) line that moves in
/// real time; a trade fires when a buyer's line meets a seller's.
/// </summary>
public sealed class AuctionTrader
{
    public int PlayerId;
    public AuctionRole Role;
    public int Quantity;
    public float Price;
    public bool IsStore;

    public bool Active => Quantity > 0;
}

public readonly record struct TradeRecord(int BuyerId, int SellerId, Resource Resource, int Price);

/// <summary>
/// A real-time double auction for a single resource — M.U.L.E.'s signature system.
/// Buyers' bid lines rise, sellers' ask lines fall, and when they cross a unit
/// trades at the midpoint. The store bounds the market: it buys surplus at a low
/// price and (for Food/Energy) sells from stock at a high price, and Smithore it
/// buys is refined into fresh MULEs.
///
/// Pure and deterministic given the same inputs, so it can run headless for tests
/// and stay in sync across a networked game. The renderer feeds one human intent
/// per frame; everything else is driven by <see cref="Update"/>.
/// </summary>
public sealed class Auction
{
    public Resource Resource { get; }
    public float PriceMin { get; }
    public float PriceMax { get; }
    public int StoreBuyPrice { get; }
    public int StoreSellPrice { get; }
    public IReadOnlyList<AuctionTrader> Traders => _traders;
    public float TimeLeft { get; private set; }
    public float Duration { get; }
    public bool IsComplete { get; private set; }
    public TradeRecord? LastTrade { get; private set; }
    public int TradeCount { get; private set; }

    private readonly List<AuctionTrader> _traders = new();
    private readonly float _driftRate;
    private float _matchCooldown;

    private int _humanPlayerId = -1;
    private int _humanIntent;

    private const float RoundSeconds = 16f;
    private const float MatchInterval = 0.22f;

    private Auction(Resource resource, float min, float max, int storeBuy, int storeSell)
    {
        Resource = resource;
        PriceMin = min;
        PriceMax = max;
        StoreBuyPrice = storeBuy;
        StoreSellPrice = storeSell;
        Duration = RoundSeconds;
        TimeLeft = RoundSeconds;
        _driftRate = MathF.Max(2f, (max - min) / 8f);
    }

    /// <summary>Per-resource reserve to hold back and target to top up to.</summary>
    private static (int reserve, int target) Policy(Resource r) => r switch
    {
        Resource.Food => (3, 4),
        Resource.Energy => (2, 3),
        _ => (0, 0) // Smithore & Crystite: sell all surplus, never bought by players
    };

    public static Auction Create(GameState state, Resource resource)
    {
        int spot = state.Prices.SpotPrice(resource);
        float min = MathF.Max(1f, spot * 0.25f);
        float max = spot * 2f + 1f;

        bool sellable = resource is Resource.Smithore or Resource.Crystite;
        int storeBuy = (int)MathF.Round(sellable ? spot * 0.9f : spot * 0.5f);
        int storeSell = (int)MathF.Round(spot * 1.5f);

        var auction = new Auction(resource, min, max, storeBuy, storeSell);
        var (reserve, target) = Policy(resource);

        foreach (var player in state.Players)
        {
            int stock = player.Store(resource);
            if (stock - reserve > 0)
            {
                auction._traders.Add(new AuctionTrader
                {
                    PlayerId = player.Id,
                    Role = AuctionRole.Seller,
                    Quantity = stock - reserve,
                    Price = max, // start greedy, drift down
                });
            }
            else if (target - stock > 0)
            {
                auction._traders.Add(new AuctionTrader
                {
                    PlayerId = player.Id,
                    Role = AuctionRole.Buyer,
                    Quantity = target - stock,
                    Price = min, // start stingy, drift up
                });
            }
        }

        // The store as a market maker.
        auction._traders.Add(new AuctionTrader
        {
            PlayerId = -1,
            Role = AuctionRole.Buyer,
            IsStore = true,
            Quantity = 9999,
            Price = storeBuy,
        });
        if (!sellable && state.Store.Stock(resource) > 0)
        {
            auction._traders.Add(new AuctionTrader
            {
                PlayerId = -1,
                Role = AuctionRole.Seller,
                IsStore = true,
                Quantity = state.Store.Stock(resource),
                Price = storeSell,
            });
        }

        return auction;
    }

    public void SetHumanIntent(int playerId, int dir)
    {
        _humanPlayerId = playerId;
        _humanIntent = Math.Sign(dir);
    }

    public void Update(GameState state, float dt)
    {
        if (IsComplete) return;

        TimeLeft -= dt;
        if (_matchCooldown > 0) _matchCooldown -= dt;

        MoveLines(dt);

        if (_matchCooldown <= 0)
            TryMatch(state);

        // End on the clock, or once no human/AI colonist can still trade
        // (only the store's perpetual orders remain).
        bool playersActive = false;
        foreach (var t in _traders)
            if (!t.IsStore && t.Active) { playersActive = true; break; }

        if (TimeLeft <= 0 || !playersActive)
            IsComplete = true;
    }

    private void MoveLines(float dt)
    {
        foreach (var t in _traders)
        {
            if (t.IsStore || !t.Active) continue;
            float autoDir = t.Role == AuctionRole.Buyer ? 1f : -1f; // toward the middle

            float speed;
            if (t.PlayerId == _humanPlayerId)
                speed = (autoDir * 0.5f + _humanIntent * 1.5f) * _driftRate;
            else
                speed = autoDir * _driftRate;

            t.Price = Math.Clamp(t.Price + speed * dt, PriceMin, PriceMax);
        }
    }

    private void TryMatch(GameState state)
    {
        AuctionTrader? buyer = null, seller = null;
        foreach (var t in _traders)
        {
            if (!t.Active) continue;
            if (t.Role == AuctionRole.Buyer && (buyer == null || t.Price > buyer.Price)) buyer = t;
            if (t.Role == AuctionRole.Seller && (seller == null || t.Price < seller.Price)) seller = t;
        }

        if (buyer == null || seller == null) return;
        if (buyer.PlayerId == seller.PlayerId && !buyer.IsStore) return; // no self-trade
        if (buyer.IsStore && seller.IsStore) return;                     // store won't trade itself
        if (buyer.Price < seller.Price) return;                          // lines haven't crossed

        int price = (int)MathF.Round((buyer.Price + seller.Price) / 2f);
        Execute(state, buyer, seller, price);

        buyer.Quantity--;
        seller.Quantity--;
        _matchCooldown = MatchInterval;
        TradeCount++;
        LastTrade = new TradeRecord(buyer.PlayerId, seller.PlayerId, Resource, price);

        // Nudge the spot price toward the trade.
        int spot = state.Prices.SpotPrice(Resource);
        state.Prices.SetSpotPrice(Resource, (int)MathF.Round((spot + price) / 2f));
    }

    private void Execute(GameState state, AuctionTrader buyer, AuctionTrader seller, int price)
    {
        if (!seller.IsStore)
        {
            var sp = state.PlayerById(seller.PlayerId);
            if (sp != null) { sp.Money += price; sp.AddStore(Resource, -1); }
        }
        else
        {
            state.Store.RemoveStock(Resource, 1);
        }

        if (!buyer.IsStore)
        {
            var bp = state.PlayerById(buyer.PlayerId);
            if (bp != null) { bp.Money -= price; bp.AddStore(Resource, 1); }
        }
        else
        {
            // The store absorbs the unit; Smithore becomes new MULEs.
            if (Resource == Resource.Smithore)
                state.Store.RefineSmithore(1);
            else
                state.Store.AddStock(Resource, 1);
        }
    }
}
