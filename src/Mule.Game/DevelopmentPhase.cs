using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Mule.Core;

namespace Mule.Game;

/// <summary>
/// Drives the real-time Development phase: the active colonist walks the planet,
/// visits the town store, claims land, and installs MULEs against a turn timer.
/// When every colonist has had a turn it resolves Production and advances the month.
///
/// All world changes route through <see cref="ColonyActions"/> so the rules stay in
/// Mule.Core. AI colonists are skipped for now (their turns end immediately).
/// </summary>
public sealed class DevelopmentPhase
{
    private enum Mode { Walking, Store, AiTurn, Auction, Summary, Event, GameOver }

    private const float TurnSeconds = 45f;
    private const float PawnSpeed = 260f; // pixels/second

    private readonly GameState _state;

    private Mode _mode = Mode.Walking;
    private int _activeIndex = -1;
    private Vector2 _pawn;
    private float _timeLeft;

    private string _message = "";
    private float _messageTime;

    private int _storeSelection;
    private IReadOnlyList<ProductionResult> _lastProduction = Array.Empty<ProductionResult>();
    private int _lastUnpowered;
    private FoodUpkeep[] _lastUpkeep = Array.Empty<FoodUpkeep>();
    private EventReport? _lastEvent;

    // AI turn playback
    private List<AiAction> _aiPlan = new();
    private int _aiStep;
    private Vector2 _aiTarget;
    private float _aiPause;

    // Auction phase
    private Auction? _auction;
    private Queue<Resource> _auctionQueue = new();
    private readonly int _humanId;

    private KeyboardState _prevKeys;
    private MapLayout _layout;

    public DevelopmentPhase(GameState state, MapLayout layout)
    {
        _state = state;
        _layout = layout;

        _humanId = -1;
        foreach (var p in state.Players)
            if (!p.IsAI) { _humanId = p.Id; break; }

        _lastUpkeep = Upkeep.ConsumeFood(_state); // month 1 upkeep before the first turn
        AdvanceToNextColonist();
    }

    public Player? ActiveColonist =>
        _activeIndex >= 0 && _activeIndex < _state.Players.Count ? _state.Players[_activeIndex] : null;

    /// <summary>Esc quits only when it isn't being used to dismiss a modal.</summary>
    public bool CanQuitOnEscape => _mode is Mode.Walking or Mode.AiTurn or Mode.Auction or Mode.GameOver;

    /// <summary>True while an auction is running, so the map is replaced by the auction view.</summary>
    public bool IsAuction => _mode == Mode.Auction;

    public bool IsGameOver => _mode == Mode.GameOver;

    /// <summary>Set when the player asks to play again from the final screen.</summary>
    public bool RestartRequested { get; private set; }

    /// <summary>Test hook: force the store open so verification tooling can capture it.</summary>
    public void DebugOpenStore() => _mode = Mode.Store;

    /// <summary>Test hook: resolve and show a colony event bulletin.</summary>
    public void DebugShowEvent()
    {
        _state.Month = 2; // past the first-month grace so an event fires
        _lastEvent = ColonyEvents.Resolve(_state);
        _mode = Mode.Event;
    }

    /// <summary>Test hook: seed some MULEs with too little energy, then show the summary.</summary>
    public void DebugShowSummary()
    {
        (int x, int y, MuleOutfit outfit)[] seed =
        {
            (3, 1, MuleOutfit.Food), (5, 1, MuleOutfit.Smithore), (6, 3, MuleOutfit.Crystite),
        };
        foreach (var (x, y, outfit) in seed)
        {
            var plot = _state.Map.At(x, y);
            plot.OwnerId = 0;
            plot.Mule = outfit;
        }
        _state.Players[0].SetStore(Resource.Energy, 1); // only enough to run one MULE

        var report = Production.Resolve(_state);
        _lastProduction = report.Yields;
        _lastUnpowered = report.UnpoweredMules;
        _state.Phase = GamePhase.Resolution;
        _mode = Mode.Summary;
    }

    /// <summary>Test hook: seed goods and jump straight into the auction sequence.</summary>
    public void DebugStartAuction()
    {
        for (int i = 0; i < _state.Players.Count; i++)
        {
            var p = _state.Players[i];
            p.SetStore(Resource.Food, i < 2 ? 6 : 0); // two sellers, two buyers
            p.SetStore(Resource.Smithore, 4);
        }
        _auctionQueue = new Queue<Resource>(new[]
        {
            Resource.Food, Resource.Energy, Resource.Smithore, Resource.Crystite
        });
        _state.Phase = GamePhase.Auction;
        BeginNextAuction();
    }

    // ---- Update ------------------------------------------------------------

    public void Update(float dt, KeyboardState keys, MapLayout layout)
    {
        _layout = layout;
        if (_messageTime > 0) _messageTime -= dt;

        switch (_mode)
        {
            case Mode.Walking: UpdateWalking(dt, keys); break;
            case Mode.Store: UpdateStore(keys); break;
            case Mode.AiTurn: UpdateAiTurn(dt); break;
            case Mode.Auction: UpdateAuction(dt, keys); break;
            case Mode.Summary: UpdateSummary(keys); break;
            case Mode.Event: UpdateEvent(keys); break;
            case Mode.GameOver:
                if (Pressed(keys, Keys.Enter) || Pressed(keys, Keys.Space)) RestartRequested = true;
                break;
        }

        _prevKeys = keys;
    }

    private void UpdateWalking(float dt, KeyboardState keys)
    {
        _timeLeft -= dt;
        if (_timeLeft <= 0)
        {
            Flash("Time's up!");
            EndColonistTurn();
            return;
        }

        var move = Vector2.Zero;
        if (keys.IsKeyDown(Keys.Left) || keys.IsKeyDown(Keys.A)) move.X -= 1;
        if (keys.IsKeyDown(Keys.Right) || keys.IsKeyDown(Keys.D)) move.X += 1;
        if (keys.IsKeyDown(Keys.Up) || keys.IsKeyDown(Keys.W)) move.Y -= 1;
        if (keys.IsKeyDown(Keys.Down) || keys.IsKeyDown(Keys.S)) move.Y += 1;

        if (move != Vector2.Zero)
        {
            move.Normalize();
            _pawn += move * PawnSpeed * dt;
            var b = _layout.Bounds;
            _pawn.X = Math.Clamp(_pawn.X, b.Left + 4, b.Right - 4);
            _pawn.Y = Math.Clamp(_pawn.Y, b.Top + 4, b.Bottom - 4);
        }

        if (Pressed(keys, Keys.Space) || Pressed(keys, Keys.E))
            DoTileAction();

        if (Pressed(keys, Keys.Enter))
        {
            Flash("Turn ended.");
            EndColonistTurn();
        }
    }

    private void DoTileAction()
    {
        var colonist = ActiveColonist;
        if (colonist == null) return;

        var (tx, ty) = _layout.TileAt(_pawn);
        if (tx < 0) return;
        var plot = _state.Map.At(tx, ty);

        if (plot.Terrain == Terrain.Town)
        {
            _mode = Mode.Store;
            _storeSelection = 0;
            return;
        }

        ActionResult result;
        if (!plot.IsOwned)
            result = ColonyActions.ClaimPlot(_state, colonist, plot);
        else if (plot.OwnerId == colonist.Id && colonist.IsLeadingMule)
            result = ColonyActions.InstallMule(_state, colonist, plot);
        else if (plot.OwnerId == colonist.Id)
            result = ActionResult.Fail("You own this plot. Buy a MULE in town to install here.");
        else
            result = ActionResult.Fail("Another colonist owns this plot.");

        Flash(result.Message);
    }

    // ---- Store modal -------------------------------------------------------

    private readonly record struct StoreOption(string Label, Func<GameState, Player, ActionResult> Act);

    private List<StoreOption> BuildStoreMenu(Player p)
    {
        var m = _state.Prices;
        var s = _state.Store;
        return new List<StoreOption>
        {
            new($"Buy Food MULE      ${s.MulePrice(MuleOutfit.Food, m)}",     (st, pl) => ColonyActions.BuyMule(st, pl, MuleOutfit.Food)),
            new($"Buy Energy MULE    ${s.MulePrice(MuleOutfit.Energy, m)}",   (st, pl) => ColonyActions.BuyMule(st, pl, MuleOutfit.Energy)),
            new($"Buy Smithore MULE  ${s.MulePrice(MuleOutfit.Smithore, m)}", (st, pl) => ColonyActions.BuyMule(st, pl, MuleOutfit.Smithore)),
            new($"Buy Crystite MULE  ${s.MulePrice(MuleOutfit.Crystite, m)}", (st, pl) => ColonyActions.BuyMule(st, pl, MuleOutfit.Crystite)),
            new($"Buy 1 Food         ${m.SpotPrice(Resource.Food)}",          (st, pl) => ColonyActions.BuyResource(st, pl, Resource.Food, 1)),
            new($"Buy 1 Energy       ${m.SpotPrice(Resource.Energy)}",        (st, pl) => ColonyActions.BuyResource(st, pl, Resource.Energy, 1)),
        };
    }

    private void UpdateStore(KeyboardState keys)
    {
        var colonist = ActiveColonist;
        if (colonist == null) { _mode = Mode.Walking; return; }
        var menu = BuildStoreMenu(colonist);

        if (Pressed(keys, Keys.Escape) || Pressed(keys, Keys.E))
        {
            _mode = Mode.Walking;
            return;
        }
        if (Pressed(keys, Keys.Up)) _storeSelection = (_storeSelection - 1 + menu.Count) % menu.Count;
        if (Pressed(keys, Keys.Down)) _storeSelection = (_storeSelection + 1) % menu.Count;

        for (int i = 0; i < menu.Count; i++)
            if (Pressed(keys, Keys.D1 + i) || Pressed(keys, Keys.NumPad1 + i))
                _storeSelection = i;

        if (Pressed(keys, Keys.Enter) || Pressed(keys, Keys.Space))
            Flash(menu[_storeSelection].Act(_state, colonist).Message);
    }

    // ---- AI turn playback --------------------------------------------------

    private void UpdateAiTurn(float dt)
    {
        var colonist = ActiveColonist;
        if (colonist == null) { EndColonistTurn(); return; }

        _timeLeft -= dt; // safety net; the AI normally finishes well within the turn
        if (_aiStep >= _aiPlan.Count || _timeLeft <= 0)
        {
            EndColonistTurn();
            return;
        }

        if (_aiPause > 0) { _aiPause -= dt; return; }

        var toTarget = _aiTarget - _pawn;
        float dist = toTarget.Length();
        float step = PawnSpeed * dt;

        if (dist <= step || dist <= 4f)
        {
            _pawn = _aiTarget;
            ApplyAiAction(colonist, _aiPlan[_aiStep]);
            _aiStep++;
            _aiPause = 0.45f;
            SetAiTarget();
        }
        else
        {
            _pawn += toTarget / dist * step;
        }
    }

    private void ApplyAiAction(Player colonist, AiAction action)
    {
        ActionResult result = action.Type switch
        {
            AiActionType.Claim => ColonyActions.ClaimPlot(_state, colonist, _state.Map.At(action.X, action.Y)),
            AiActionType.Buy => ColonyActions.BuyMule(_state, colonist, action.Outfit),
            AiActionType.Install => ColonyActions.InstallMule(_state, colonist, _state.Map.At(action.X, action.Y)),
            _ => ActionResult.Fail("")
        };
        if (result.Message.Length > 0)
            Flash($"{colonist.Name}: {result.Message}");
    }

    private void SetAiTarget()
    {
        if (_aiStep < _aiPlan.Count)
        {
            var a = _aiPlan[_aiStep];
            _aiTarget = _layout.PlotCenter(a.X, a.Y);
        }
    }

    // ---- Turn / phase flow -------------------------------------------------

    private void EndColonistTurn() => AdvanceToNextColonist();

    private void AdvanceToNextColonist()
    {
        int next = _activeIndex + 1;
        if (next < _state.Players.Count)
            BeginColonistTurn(next);
        else
            ResolveMonth();
    }

    private void BeginColonistTurn(int index)
    {
        _activeIndex = index;
        _state.ActivePlayerIndex = index;
        _state.Phase = GamePhase.Development;
        _pawn = TownCenter();

        var colonist = _state.Players[index];
        _timeLeft = TurnSeconds * colonist.TimeFactor;

        if (colonist.IsAI)
        {
            _aiPlan = AiPlanner.PlanTurn(_state, colonist);
            _aiStep = 0;
            _aiPause = 0.5f;
            SetAiTarget();
            _mode = Mode.AiTurn;
            Flash($"{colonist.Name} is developing...");
        }
        else
        {
            _mode = Mode.Walking;
            if (colonist.TimeFactor < 0.999f)
                Flash($"Short on food - turn cut to {(int)MathF.Round(colonist.TimeFactor * 100)}%.");
        }
    }

    private void ResolveMonth()
    {
        _state.Phase = GamePhase.Production;
        var report = Production.Resolve(_state);
        _lastProduction = report.Yields;
        _lastUnpowered = report.UnpoweredMules;

        // Sell the harvest: auction each resource in turn, then wrap up the month.
        _auctionQueue = new Queue<Resource>(new[]
        {
            Resource.Food, Resource.Energy, Resource.Smithore, Resource.Crystite
        });
        _state.Phase = GamePhase.Auction;
        BeginNextAuction();
    }

    private void BeginNextAuction()
    {
        if (_auctionQueue.Count == 0)
        {
            EndMonth();
            return;
        }
        _auction = Auction.Create(_state, _auctionQueue.Dequeue());
        _mode = Mode.Auction;
    }

    private void UpdateAuction(float dt, KeyboardState keys)
    {
        if (_auction == null) { BeginNextAuction(); return; }

        int dir = 0;
        if (keys.IsKeyDown(Keys.Up)) dir += 1;
        if (keys.IsKeyDown(Keys.Down)) dir -= 1;
        _auction.SetHumanIntent(_humanId, dir);

        _auction.Update(_state, dt);

        if (Pressed(keys, Keys.Enter))       // let the player skip ahead
            _auction = null;
        else if (_auction != null && _auction.IsComplete)
            _auction = null;

        if (_auction == null)
            BeginNextAuction();
    }

    private void EndMonth()
    {
        if (_state.Month >= _state.TotalMonths)
        {
            _state.Phase = GamePhase.GameOver;
            _mode = Mode.GameOver;
        }
        else
        {
            _state.Phase = GamePhase.Resolution;
            _mode = Mode.Summary;
        }
    }

    private void UpdateSummary(KeyboardState keys)
    {
        if (Pressed(keys, Keys.Enter) || Pressed(keys, Keys.Space))
        {
            _state.Month++;
            _lastUpkeep = Upkeep.ConsumeFood(_state); // eat before the new month's turns
            _lastEvent = ColonyEvents.Resolve(_state);

            if (_lastEvent.HasValue)
                _mode = Mode.Event; // show the news bulletin, then start turns
            else
                StartNewMonthTurns();
        }
    }

    private void UpdateEvent(KeyboardState keys)
    {
        if (Pressed(keys, Keys.Enter) || Pressed(keys, Keys.Space))
            StartNewMonthTurns();
    }

    private void StartNewMonthTurns()
    {
        _activeIndex = -1;
        AdvanceToNextColonist();
    }

    private Vector2 TownCenter()
    {
        foreach (var plot in _state.Map.AllPlots())
            if (plot.Terrain == Terrain.Town)
                return _layout.Cell > 0 ? _layout.PlotCenter(plot.X, plot.Y)
                                        : new Vector2(_layout.OriginX, _layout.OriginY);
        return new Vector2(_layout.OriginX, _layout.OriginY);
    }

    private bool Pressed(KeyboardState keys, Keys key) => keys.IsKeyDown(key) && _prevKeys.IsKeyUp(key);

    private void Flash(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        _message = message;
        _messageTime = 3f;
    }

    // ---- Rendering ---------------------------------------------------------

    /// <summary>Pawn, timer bar and status message, drawn over the map.</summary>
    public void DrawWorld(SpriteBatch batch, ShapeBatch shapes, SpriteFont font)
    {
        if (_mode == Mode.Auction)
        {
            DrawAuction(batch, shapes, font);
            return;
        }

        // The colonist pawn.
        if (_mode is Mode.Walking or Mode.Store or Mode.AiTurn)
        {
            var colonist = ActiveColonist;
            if (colonist != null)
            {
                var color = Palette.FromPacked(colonist.Color);
                var body = new Rectangle((int)_pawn.X - 9, (int)_pawn.Y - 9, 18, 18);
                shapes.Fill(body, color);
                shapes.Outline(body, Palette.Background, 2);
                if (colonist.IsLeadingMule)
                {
                    var tag = new Rectangle(body.Right - 4, body.Top - 6, 12, 12);
                    shapes.Fill(tag, Palette.MuleColor(colonist.CarriedMule));
                    shapes.Outline(tag, Palette.Background, 2);
                }
            }
        }

        // Timer bar across the top of the map area.
        if (_mode is Mode.Walking)
        {
            var b = _layout.Bounds;
            int barH = 6;
            var bg = new Rectangle(b.Left, b.Top - 14, b.Width, barH);
            shapes.Fill(bg, Palette.PanelLight);
            float pct = Math.Clamp(_timeLeft / TurnSeconds, 0f, 1f);
            var fill = new Rectangle(b.Left, b.Top - 14, (int)(b.Width * pct), barH);
            shapes.Fill(fill, pct > 0.25f ? Palette.Food : Palette.Crystite);
        }

        // Transient status message.
        if (_messageTime > 0 && _message.Length > 0)
        {
            var b = _layout.Bounds;
            var size = font.MeasureString(_message);
            var pos = new Vector2(b.Center.X - size.X / 2, b.Bottom + 10);
            shapes.Fill(new Rectangle((int)pos.X - 10, (int)pos.Y - 4, (int)size.X + 20, (int)size.Y + 8), Palette.Panel);
            batch.DrawString(font, _message, pos, Palette.Text);
        }
    }

    private float PriceToY(Rectangle area, float price)
    {
        float t = (price - _auction!.PriceMin) / MathF.Max(1f, _auction.PriceMax - _auction.PriceMin);
        return area.Bottom - t * area.Height;
    }

    private void DrawAuction(SpriteBatch batch, ShapeBatch shapes, SpriteFont font)
    {
        if (_auction == null) return;

        // Cover the map region with a clean panel.
        var canvas = _layout.Bounds;
        shapes.Fill(canvas, Palette.Background);

        // Header: resource name + swatch + time bar.
        var accent = Palette.ResourceColor(_auction.Resource);
        shapes.Fill(new Rectangle(canvas.Left, canvas.Top, 16, 16), accent);
        batch.DrawString(font, $"{_auction.Resource} AUCTION", new Vector2(canvas.Left + 26, canvas.Top - 2),
            Palette.Text, 0f, Vector2.Zero, 1.3f, SpriteEffects.None, 0f);

        var timerBg = new Rectangle(canvas.Left, canvas.Top + 26, canvas.Width, 5);
        shapes.Fill(timerBg, Palette.PanelLight);
        float tpct = Math.Clamp(_auction.TimeLeft / _auction.Duration, 0f, 1f);
        shapes.Fill(new Rectangle(canvas.Left, canvas.Top + 26, (int)(canvas.Width * tpct), 5), accent);

        // Price chart area (leave a left gutter for axis labels).
        int gutter = 96;
        var chart = new Rectangle(canvas.Left + gutter, canvas.Top + 48, canvas.Width - gutter - 12, canvas.Height - 92);
        shapes.Fill(chart, Palette.Panel);
        shapes.Outline(chart, Palette.Grid, 1);

        // Store reference lines.
        DrawPriceLine(batch, shapes, font, chart, _auction.StoreSellPrice, Palette.TextMuted, "store sells");
        DrawPriceLine(batch, shapes, font, chart, _auction.StoreBuyPrice, Palette.TextMuted, "store buys");

        // Player columns: buyers left third, sellers right third. The store sits at
        // the far edge as the market "wall" on its two price lines.
        int buyerX = chart.Left + chart.Width / 3;
        int sellerX = chart.Left + chart.Width * 2 / 3;
        batch.DrawString(font, "BUYERS", new Vector2(buyerX - 28, chart.Bottom + 8), Palette.TextMuted);
        batch.DrawString(font, "SELLERS", new Vector2(sellerX - 30, chart.Bottom + 8), Palette.TextMuted);

        var buyers = new List<AuctionTrader>();
        var sellers = new List<AuctionTrader>();
        foreach (var t in _auction.Traders)
        {
            if (!t.Active) continue;
            if (t.IsStore) DrawTraderMarker(batch, shapes, font, chart.Right - 20, t, chart);
            else (t.Role == AuctionRole.Buyer ? buyers : sellers).Add(t);
        }

        DrawTraderColumn(batch, shapes, font, buyers, buyerX, chart);
        DrawTraderColumn(batch, shapes, font, sellers, sellerX, chart);

        // Last-trade ticker.
        if (_auction.LastTrade is { } lt)
        {
            string who = lt.SellerId < 0 ? "store" : _state.PlayerById(lt.SellerId)?.Name ?? "?";
            string to = lt.BuyerId < 0 ? "store" : _state.PlayerById(lt.BuyerId)?.Name ?? "?";
            batch.DrawString(font, $"Last: {who} -> {to}  1 {_auction.Resource} @ ${lt.Price}   ({_auction.TradeCount} trades)",
                new Vector2(canvas.Left, canvas.Bottom - 20), Palette.TextMuted);
        }
    }

    private void DrawTraderColumn(SpriteBatch batch, ShapeBatch shapes, SpriteFont font,
        List<AuctionTrader> traders, int centerX, Rectangle chart)
    {
        int n = traders.Count;
        for (int i = 0; i < n; i++)
        {
            int x = centerX + (int)((i - (n - 1) / 2f) * 72f); // centered fan-out
            DrawTraderMarker(batch, shapes, font, x, traders[i], chart);
        }
    }

    private void DrawTraderMarker(SpriteBatch batch, ShapeBatch shapes, SpriteFont font,
        int x, AuctionTrader t, Rectangle chart)
    {
        int y = (int)Math.Clamp(PriceToY(chart, t.Price), chart.Top + 8, chart.Bottom - 8);
        bool isHuman = !t.IsStore && t.PlayerId == _humanId;
        Color color = t.IsStore ? Palette.PanelLight : Palette.FromPacked(_state.PlayerById(t.PlayerId)!.Color);

        var marker = new Rectangle(x - 11, y - 11, 22, 22);
        shapes.Fill(marker, color);
        shapes.Outline(marker, isHuman ? Palette.Text : Palette.Background, isHuman ? 3 : 2);

        string label = t.IsStore ? "store" : (isHuman ? "YOU" : _state.PlayerById(t.PlayerId)!.Name);
        if (!t.IsStore) label += $" x{t.Quantity}";
        batch.DrawString(font, label, new Vector2(x - 12, y + 14), t.IsStore ? Palette.TextMuted : color);
    }

    private void DrawPriceLine(SpriteBatch batch, ShapeBatch shapes, SpriteFont font, Rectangle chart, int price, Color color, string label)
    {
        int y = (int)Math.Clamp(PriceToY(chart, price), chart.Top, chart.Bottom);
        for (int x = chart.Left; x < chart.Right; x += 12)  // dashed
            shapes.Fill(new Rectangle(x, y, 6, 1), color);
        batch.DrawString(font, $"${price} {label}", new Vector2(chart.Left - 90, y - 8), color);
    }

    /// <summary>Full-screen modal overlays (store, month summary, game over).</summary>
    public void DrawModal(SpriteBatch batch, ShapeBatch shapes, SpriteFont font, int screenW, int screenH)
    {
        if (_mode == Mode.Store) DrawStore(batch, shapes, font, screenW, screenH);
        else if (_mode == Mode.Event) DrawEvent(batch, shapes, font, screenW, screenH);
        else if (_mode == Mode.Summary) DrawSummary(batch, shapes, font, screenW, screenH, "Month Complete", true);
        else if (_mode == Mode.GameOver) DrawSummary(batch, shapes, font, screenW, screenH, "Game Over", false);
    }

    private void DrawStore(SpriteBatch batch, ShapeBatch shapes, SpriteFont font, int screenW, int screenH)
    {
        var colonist = ActiveColonist;
        if (colonist == null) return;
        var menu = BuildStoreMenu(colonist);

        shapes.Fill(new Rectangle(0, 0, screenW, screenH), new Color(0, 0, 0, 150));

        int w = 460, h = 360;
        var panel = new Rectangle((screenW - w) / 2, (screenH - h) / 2, w, h);
        shapes.Fill(panel, Palette.Panel);
        shapes.Outline(panel, Palette.Grid, 2);

        int x = panel.X + 24;
        int y = panel.Y + 20;
        batch.DrawString(font, "TOWN STORE", new Vector2(x, y), Palette.Text, 0f, Vector2.Zero, 1.3f, SpriteEffects.None, 0f);
        y += 34;
        batch.DrawString(font, $"Cash ${colonist.Money}      MULEs in stock: {_state.Store.MulesAvailable}",
            new Vector2(x, y), Palette.TextMuted);
        y += 34;

        for (int i = 0; i < menu.Count; i++)
        {
            var row = new Rectangle(x - 8, y - 3, w - 32, 30);
            if (i == _storeSelection) shapes.Fill(row, Palette.PanelLight);
            batch.DrawString(font, $"{i + 1}. {menu[i].Label}", new Vector2(x, y),
                i == _storeSelection ? Palette.Text : Palette.TextMuted);
            y += 32;
        }

        y += 6;
        batch.DrawString(font, "Up/Down + Enter to buy  ·  1-6 quick pick  ·  Esc to leave".Replace("·", "-"),
            new Vector2(x, y), Palette.TextMuted);

        if (colonist.IsLeadingMule)
        {
            y += 26;
            batch.DrawString(font, $"Leading a {colonist.CarriedMule} MULE - walk it to your land and press Space.",
                new Vector2(x, y), Palette.MuleColor(colonist.CarriedMule));
        }
    }

    private void DrawEvent(SpriteBatch batch, ShapeBatch shapes, SpriteFont font, int screenW, int screenH)
    {
        if (_lastEvent is not { } ev) return;

        shapes.Fill(new Rectangle(0, 0, screenW, screenH), new Color(0, 0, 0, 170));

        int w = 560, h = 260;
        var panel = new Rectangle((screenW - w) / 2, (screenH - h) / 2, w, h);
        shapes.Fill(panel, Palette.Panel);
        shapes.Outline(panel, Palette.Grid, 2);

        // A colored banner keys the mood: green for fortune, amber for trouble.
        var accent = ev.IsGood ? Palette.Food : Palette.Energy;
        shapes.Fill(new Rectangle(panel.X, panel.Y, panel.Width, 6), accent);

        int x = panel.X + 28;
        int y = panel.Y + 26;
        batch.DrawString(font, $"COLONY NEWS  -  Month {_state.Month}", new Vector2(x, y), Palette.TextMuted);
        y += 34;
        batch.DrawString(font, ev.Headline, new Vector2(x, y), accent, 0f, Vector2.Zero, 1.6f, SpriteEffects.None, 0f);
        y += 50;

        // Affected-player swatch, if the event singled someone out.
        if (ev.AffectedPlayerId >= 0)
        {
            var p = _state.PlayerById(ev.AffectedPlayerId);
            if (p != null)
                shapes.Fill(new Rectangle(x, y + 2, 12, 12), Palette.FromPacked(p.Color));
        }
        batch.DrawString(font, ev.Detail, new Vector2(x + (ev.AffectedPlayerId >= 0 ? 22 : 0), y), Palette.Text);

        batch.DrawString(font, "Press Enter to continue", new Vector2(x, panel.Bottom - 40), Palette.TextMuted);
    }

    private void DrawSummary(SpriteBatch batch, ShapeBatch shapes, SpriteFont font, int screenW, int screenH, string title, bool cont)
    {
        shapes.Fill(new Rectangle(0, 0, screenW, screenH), new Color(0, 0, 0, 170));

        int w = 520, h = 420;
        var panel = new Rectangle((screenW - w) / 2, (screenH - h) / 2, w, h);
        shapes.Fill(panel, Palette.Panel);
        shapes.Outline(panel, Palette.Grid, 2);

        int x = panel.X + 28;
        int y = panel.Y + 22;
        batch.DrawString(font, title, new Vector2(x, y), Palette.Text, 0f, Vector2.Zero, 1.5f, SpriteEffects.None, 0f);
        y += 44;

        if (cont)
        {
            batch.DrawString(font, "Production this month:", new Vector2(x, y), Palette.TextMuted);
            y += 30;
            if (_lastProduction.Count == 0)
            {
                batch.DrawString(font, "  No MULEs were producing.", new Vector2(x, y), Palette.TextMuted);
                y += 26;
            }
            foreach (var r in _lastProduction)
            {
                var owner = _state.PlayerById(r.PlayerId);
                shapes.Fill(new Rectangle(x, y + 3, 10, 10), Palette.ResourceColor(r.Resource));
                batch.DrawString(font, $"  {owner?.Name}: +{r.Amount} {r.Resource}", new Vector2(x + 18, y), Palette.Text);
                y += 24;
            }

            if (_lastUnpowered > 0)
            {
                y += 6;
                shapes.Fill(new Rectangle(x, y + 3, 10, 10), Palette.Energy);
                batch.DrawString(font, $"  {_lastUnpowered} MULE(s) idle - not enough Energy to run them",
                    new Vector2(x + 18, y), Palette.Energy);
                y += 30;
            }

            batch.DrawString(font, $"Next month each colonist must eat {Upkeep.FoodNeeded(_state.Month + 1)} Food.",
                new Vector2(x, panel.Bottom - 72), Palette.TextMuted);
        }
        else
        {
            // Final standings by score.
            var ranked = new List<Player>(_state.Players);
            ranked.Sort((a, b) => b.Score(_state).CompareTo(a.Score(_state)));
            for (int i = 0; i < ranked.Count; i++)
            {
                shapes.Fill(new Rectangle(x, y + 2, 12, 12), Palette.FromPacked(ranked[i].Color));
                batch.DrawString(font, $"  {i + 1}. {ranked[i].Name} - {ranked[i].Score(_state)} pts",
                    new Vector2(x + 20, y), i == 0 ? Palette.Text : Palette.TextMuted);
                y += 28;
            }
        }

        string footer = cont ? "Press Enter to start the next month" : "Press Enter to play again";
        batch.DrawString(font, footer, new Vector2(x, panel.Bottom - 40), Palette.TextMuted);
    }
}
