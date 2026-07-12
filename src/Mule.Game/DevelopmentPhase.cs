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
    private enum Mode { Walking, Store, AiTurn, Summary, GameOver }

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

    // AI turn playback
    private List<AiAction> _aiPlan = new();
    private int _aiStep;
    private Vector2 _aiTarget;
    private float _aiPause;

    private KeyboardState _prevKeys;
    private MapLayout _layout;

    public DevelopmentPhase(GameState state, MapLayout layout)
    {
        _state = state;
        _layout = layout;
        AdvanceToNextColonist();
    }

    public Player? ActiveColonist =>
        _activeIndex >= 0 && _activeIndex < _state.Players.Count ? _state.Players[_activeIndex] : null;

    /// <summary>Esc quits only when it isn't being used to dismiss a modal.</summary>
    public bool CanQuitOnEscape => _mode is Mode.Walking or Mode.AiTurn or Mode.GameOver;

    /// <summary>Test hook: force the store open so verification tooling can capture it.</summary>
    public void DebugOpenStore() => _mode = Mode.Store;

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
            case Mode.Summary: UpdateSummary(keys); break;
            case Mode.GameOver: break;
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
        _timeLeft = TurnSeconds;
        _pawn = TownCenter();

        var colonist = _state.Players[index];
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
        }
    }

    private void ResolveMonth()
    {
        _state.Phase = GamePhase.Production;
        _lastProduction = Production.Resolve(_state);

        if (_state.Month >= _state.TotalMonths)
        {
            _state.Phase = GamePhase.GameOver;
            _mode = Mode.GameOver;
        }
        else
        {
            _mode = Mode.Summary;
        }
    }

    private void UpdateSummary(KeyboardState keys)
    {
        if (Pressed(keys, Keys.Enter) || Pressed(keys, Keys.Space))
        {
            _state.Month++;
            _activeIndex = -1;
            AdvanceToNextColonist();
        }
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

    /// <summary>Full-screen modal overlays (store, month summary, game over).</summary>
    public void DrawModal(SpriteBatch batch, ShapeBatch shapes, SpriteFont font, int screenW, int screenH)
    {
        if (_mode == Mode.Store) DrawStore(batch, shapes, font, screenW, screenH);
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

        string footer = cont ? "Press Enter to start the next month" : "Press Esc to quit";
        batch.DrawString(font, footer, new Vector2(x, panel.Bottom - 40), Palette.TextMuted);
    }
}
