using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Mule.Core;

namespace Mule.Game;

public class Game1 : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private ShapeBatch _shapes = null!;
    private SpriteFont _font = null!;

    private GameState _state = null!;
    private DevelopmentPhase _dev = null!;
    private MapLayout _layout;

    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;
    private const int SidebarWidth = 340;
    private const int HeaderHeight = 64;
    private const int Margin = 24;

    // Optional headless screenshot: set MULE_SCREENSHOT=<path> to capture one
    // frame to a PNG and exit. Used for automated visual verification.
    private readonly string? _screenshotPath = Environment.GetEnvironmentVariable("MULE_SCREENSHOT");
    private int _frame;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = WindowWidth,
            PreferredBackBufferHeight = WindowHeight,
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "M.U.L.E. — Colony";
    }

    private Rectangle MapArea => new(
        Margin,
        HeaderHeight + Margin,
        WindowWidth - SidebarWidth - Margin * 2,
        WindowHeight - HeaderHeight - Margin * 2 - 32);

    protected override void Initialize()
    {
        // Deterministic seed for now; will be chosen at setup / shared over the network.
        int humans = Environment.GetEnvironmentVariable("MULE_ALLAI") != null ? 0 : 1;
        _state = GameFactory.NewGame(seed: 1983, humanPlayers: humans, totalPlayers: 4, totalMonths: 12);
        _layout = new MapLayout(_state.Map, MapArea);
        _dev = new DevelopmentPhase(_state, _layout);
        if (Environment.GetEnvironmentVariable("MULE_OPENSTORE") != null)
            _dev.DebugOpenStore();
        if (Environment.GetEnvironmentVariable("MULE_AUCTION") != null)
            _dev.DebugStartAuction();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _shapes = new ShapeBatch(GraphicsDevice, _spriteBatch);
        _font = Content.Load<SpriteFont>("UIFont");
    }

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();
        if (_dev.CanQuitOnEscape &&
            (keys.IsKeyDown(Keys.Escape) || GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed))
        {
            Exit();
            return;
        }

        _layout = new MapLayout(_state.Map, MapArea);
        _dev.Update((float)gameTime.ElapsedGameTime.TotalSeconds, keys, _layout);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.Background);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        DrawHeader();
        if (!_dev.IsAuction) DrawMap();
        _dev.DrawWorld(_spriteBatch, _shapes, _font);
        DrawSidebar();
        _dev.DrawModal(_spriteBatch, _shapes, _font, WindowWidth, WindowHeight);
        DrawFooter();

        _spriteBatch.End();

        int shotFrame = int.TryParse(Environment.GetEnvironmentVariable("MULE_SHOTFRAME"), out var f) ? f : 3;
        if (_screenshotPath != null && ++_frame == shotFrame)
        {
            CaptureScreenshot(_screenshotPath);
            Exit();
        }

        base.Draw(gameTime);
    }

    private void DrawHeader()
    {
        var bar = new Rectangle(0, 0, WindowWidth, HeaderHeight);
        _shapes.Fill(bar, Palette.Panel);
        _shapes.Fill(0, HeaderHeight - 2, WindowWidth, 2, Palette.Grid);

        _spriteBatch.DrawString(_font, "M . U . L . E .", new Vector2(Margin, 14), Palette.Text, 0f,
            Vector2.Zero, 1.6f, SpriteEffects.None, 0f);

        string phase = $"Month {_state.Month} / {_state.TotalMonths}      Phase: {_state.Phase}";
        var size = _font.MeasureString(phase);
        _spriteBatch.DrawString(_font, phase,
            new Vector2(WindowWidth - SidebarWidth - Margin - size.X, 22), Palette.TextMuted);
    }

    private void DrawMap()
    {
        foreach (var plot in _state.Map.AllPlots())
        {
            var rect = _layout.PlotRect(plot.X, plot.Y);
            _shapes.Fill(rect, Palette.TerrainColor(plot.Terrain));

            if (plot.Terrain == Terrain.Town)
            {
                var label = "TOWN";
                var ls = _font.MeasureString(label);
                _spriteBatch.DrawString(_font, label,
                    new Vector2(rect.Center.X - ls.X / 2, rect.Center.Y - ls.Y / 2), Palette.Text);
            }

            if (plot.IsOwned)
            {
                var owner = _state.PlayerById(plot.OwnerId);
                if (owner != null)
                    _shapes.Outline(rect, Palette.FromPacked(owner.Color), 3);
            }
            else
            {
                _shapes.Outline(rect, Palette.Grid, 1);
            }

            if (plot.HasMule)
            {
                int d = Math.Max(8, _layout.Cell / 5);
                var dot = new Rectangle(rect.Center.X - d / 2, rect.Center.Y - d / 2, d, d);
                _shapes.Fill(dot, Palette.MuleColor(plot.Mule));
                _shapes.Outline(dot, Palette.Background, 2);
            }
        }
    }

    private void DrawSidebar()
    {
        int x = WindowWidth - SidebarWidth;
        var panel = new Rectangle(x, HeaderHeight, SidebarWidth, WindowHeight - HeaderHeight);
        _shapes.Fill(panel, Palette.Panel);
        _shapes.Fill(x, HeaderHeight, 2, panel.Height, Palette.Grid);

        int pad = 18;
        int cardX = x + pad;
        int cardW = SidebarWidth - pad * 2;
        int cardH = 118;
        int y = HeaderHeight + pad;

        var active = _dev.ActiveColonist;

        foreach (var p in _state.Players)
        {
            var card = new Rectangle(cardX, y, cardW, cardH);
            _shapes.Fill(card, Palette.PanelLight);
            _shapes.Outline(card, p == active ? Palette.FromPacked(p.Color) : Palette.Grid, p == active ? 2 : 1);

            _shapes.Fill(new Rectangle(cardX + 12, y + 14, 14, 14), Palette.FromPacked(p.Color));
            _spriteBatch.DrawString(_font, p.Name, new Vector2(cardX + 34, y + 12), Palette.Text);

            string tag = p.IsAI ? "AI" : (p == active ? "TURN" : "");
            if (tag.Length > 0)
            {
                var ts = _font.MeasureString(tag);
                _spriteBatch.DrawString(_font, tag, new Vector2(card.Right - 14 - ts.X, y + 12),
                    p == active ? Palette.Food : Palette.TextMuted);
            }

            _spriteBatch.DrawString(_font, $"${p.Money}", new Vector2(cardX + 12, y + 36), Palette.Text);
            _spriteBatch.DrawString(_font, $"Score {p.Score(_state)}",
                new Vector2(card.Right - 118, y + 36), Palette.TextMuted);

            int ry = y + 66;
            int col = 0;
            foreach (Resource r in Enum.GetValues<Resource>())
            {
                int rx = cardX + 12 + col * 78;
                _shapes.Fill(new Rectangle(rx, ry + 3, 10, 10), Palette.ResourceColor(r));
                _spriteBatch.DrawString(_font, $"{r.ToString()[0]} {p.Store(r)}",
                    new Vector2(rx + 16, ry), Palette.TextMuted);
                col++;
                if (col == 2) { col = 0; ry += 22; }
            }

            y += cardH + 12;
        }
    }

    private void DrawFooter()
    {
        string hint = _dev.IsAuction
            ? "Up/Down: raise or lower your price   |   trades fire when a buyer meets a seller   |   Enter: skip   |   Esc: quit"
            : "WASD/Arrows: move   |   Space: claim land / enter store / install MULE   |   Enter: end turn   |   Esc: quit";
        _spriteBatch.DrawString(_font, hint, new Vector2(Margin, WindowHeight - 28), Palette.TextMuted);
    }

    private void CaptureScreenshot(string path)
    {
        int w = GraphicsDevice.PresentationParameters.BackBufferWidth;
        int h = GraphicsDevice.PresentationParameters.BackBufferHeight;
        var data = new Color[w * h];
        GraphicsDevice.GetBackBufferData(data);
        using var tex = new Texture2D(GraphicsDevice, w, h);
        tex.SetData(data);
        using var fs = System.IO.File.Create(path);
        tex.SaveAsPng(fs, w, h);
    }
}
