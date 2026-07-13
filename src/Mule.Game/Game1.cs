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

    private SetupScreen _setup = null!;
    private bool _inSetup = true;
    private KeyboardState _prevKeys;
    private float _time; // seconds elapsed, for ambient animation (water)
    private Starfield _starfield = null!;
    private Sfx _sfx = null!;
    private Music _music = null!;

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
        _setup = new SetupScreen();
        _starfield = new Starfield();
        _sfx = new Sfx();
        _music = new Music();
        _music.Play(); // title theme over the setup screen

        // Verification hooks skip setup and drive a default game directly.
        bool autoStart = Env("MULE_OPENSTORE") || Env("MULE_AUCTION") ||
                         Env("MULE_SUMMARY") || Env("MULE_EVENT") || Env("MULE_ALLAI");
        if (autoStart)
        {
            StartGame(new GameConfig
            {
                TotalPlayers = 4,
                Humans = Env("MULE_ALLAI") ? 0 : 1,
                Months = 12,
                Seed = 1983,
                StartMoney = 1000,
                DifficultyName = "Standard",
            });
            if (Env("MULE_OPENSTORE")) _dev.DebugOpenStore();
            if (Env("MULE_AUCTION")) _dev.DebugStartAuction();
            if (Env("MULE_SUMMARY")) _dev.DebugShowSummary();
            if (Env("MULE_EVENT")) _dev.DebugShowEvent();
        }

        base.Initialize();
    }

    private static bool Env(string name) => Environment.GetEnvironmentVariable(name) != null;

    private void StartGame(GameConfig cfg)
    {
        _state = GameFactory.NewGame(cfg.Seed, cfg.Humans, cfg.TotalPlayers, cfg.Months, cfg.StartMoney);
        _layout = new MapLayout(_state.Map, MapArea);
        _dev = new DevelopmentPhase(_state, _layout, _sfx);
        _inSetup = false;
        _music.Stop(); // quiet the title theme once the colony begins
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
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _time += dt;
        _starfield.Update(dt);

        // Edge-triggered so a single held Esc can't cascade (back to menu, then quit).
        bool escPressed = keys.IsKeyDown(Keys.Escape) && _prevKeys.IsKeyUp(Keys.Escape);
        bool backPressed = GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed;
        _prevKeys = keys;

        if (_inSetup)
        {
            if (escPressed) Exit(); // Esc quits only from the title menu
            else if (_setup.Update(keys, _sfx)) StartGame(_setup.BuildConfig());
            base.Update(gameTime);
            return;
        }

        // In-game, Esc backs out to the title menu rather than quitting the app.
        if ((escPressed || backPressed) && _dev.CanQuitOnEscape)
        {
            ReturnToMenu();
            base.Update(gameTime);
            return;
        }

        _layout = new MapLayout(_state.Map, MapArea);
        _dev.Update((float)gameTime.ElapsedGameTime.TotalSeconds, keys, _layout);

        if (_dev.RestartRequested)
            ReturnToMenu();

        base.Update(gameTime);
    }

    private void ReturnToMenu()
    {
        _setup = new SetupScreen();
        _inSetup = true;
        _music.Play(); // back to the title theme
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.Background);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        if (_inSetup)
        {
            _setup.Draw(_spriteBatch, _shapes, _font, _starfield, WindowWidth, WindowHeight);
            _spriteBatch.End();
            CaptureIfRequested();
            base.Draw(gameTime);
            return;
        }

        DrawHeader();
        if (!_dev.IsAuction) DrawMap();
        _dev.DrawWorld(_spriteBatch, _shapes, _font);
        DrawSidebar();
        _dev.DrawModal(_spriteBatch, _shapes, _font, WindowWidth, WindowHeight);
        DrawFooter();

        _spriteBatch.End();
        CaptureIfRequested();
        base.Draw(gameTime);
    }

    private void CaptureIfRequested()
    {
        if (_screenshotPath == null) return;
        int shotFrame = int.TryParse(Environment.GetEnvironmentVariable("MULE_SHOTFRAME"), out var f) ? f : 3;
        if (++_frame == shotFrame)
        {
            CaptureScreenshot(_screenshotPath);
            Exit();
        }
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

            if (plot.Terrain == Terrain.River)
                DrawWater(_layout.FullRect(plot.X, plot.Y)); // fill the whole cell so water joins up
            else
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
            else if (plot.Terrain != Terrain.River)
            {
                _shapes.Outline(rect, Palette.Grid, 1); // rivers have no grid line, so they flow
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

    private static readonly Color WaterDeep = new(0x1B, 0x3E, 0x4E);
    private static readonly Color WaterRipple = new(0x4C, 0x82, 0x99);
    private static readonly Color WaterGlint = new(0x7E, 0xB6, 0xC8);

    /// <summary>Draws a river cell as animated water: a deep base with drifting ripples.</summary>
    private void DrawWater(Rectangle cell)
    {
        _shapes.Fill(cell, WaterDeep);

        // A few horizontal ripple lines that undulate and drift over time.
        int rows = Math.Max(3, cell.Height / 14);
        int seg = 3;
        for (int r = 0; r < rows; r++)
        {
            float baseY = cell.Top + cell.Height * (r + 0.5f) / rows;
            float rowPhase = r * 1.7f;
            bool glint = r % 2 == 0;
            for (int x = cell.Left; x < cell.Right; x += seg)
            {
                float wave = MathF.Sin(x * 0.16f + _time * 1.8f + rowPhase);
                float y = baseY + wave * 2.4f;
                // Fade ripple in and out along its length for a soft, watery look.
                float a = 0.35f + 0.4f * (0.5f + 0.5f * MathF.Sin(x * 0.09f - _time * 1.3f + rowPhase));
                _shapes.Fill(x, y, seg, 1.7f, (glint ? WaterGlint : WaterRipple) * a);
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
            ? "Up/Down: raise or lower your price   |   trades fire when a buyer meets a seller   |   Enter: skip   |   Esc: menu"
            : "WASD/Arrows: move   |   Space: claim land / enter store / install MULE   |   Enter: end turn   |   Esc: menu";
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
