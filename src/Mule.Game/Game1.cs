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
    private bool _confirmQuit; // showing the "quit to menu?" prompt over a paused game
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
                Seed = int.TryParse(Environment.GetEnvironmentVariable("MULE_SEED"), out var s) ? s : 1983,
                StartMoney = 1000,
                DifficultyName = "Standard",
            });
            if (Env("MULE_OPENSTORE")) _dev.DebugOpenStore();
            if (Env("MULE_AUCTION")) _dev.DebugStartAuction();
            if (Env("MULE_SUMMARY")) _dev.DebugShowSummary();
            if (Env("MULE_EVENT")) _dev.DebugShowEvent();
            if (Env("MULE_QUITCONFIRM")) _confirmQuit = true;
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
        _confirmQuit = false;
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

        // Edge-triggered so a single held key can't cascade across states.
        bool escPressed = keys.IsKeyDown(Keys.Escape) && _prevKeys.IsKeyUp(Keys.Escape);
        bool enterPressed = keys.IsKeyDown(Keys.Enter) && _prevKeys.IsKeyUp(Keys.Enter);
        bool yPressed = keys.IsKeyDown(Keys.Y) && _prevKeys.IsKeyUp(Keys.Y);
        bool nPressed = keys.IsKeyDown(Keys.N) && _prevKeys.IsKeyUp(Keys.N);
        bool backPressed = GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed;
        _prevKeys = keys;

        if (_inSetup)
        {
            if (escPressed) Exit(); // Esc quits only from the title menu
            else if (_setup.Update(keys, _sfx)) StartGame(_setup.BuildConfig());
            base.Update(gameTime);
            return;
        }

        // While the quit prompt is up, the game is paused pending a yes/no answer.
        if (_confirmQuit)
        {
            if (yPressed || enterPressed) { _confirmQuit = false; ReturnToMenu(); }
            else if (nPressed || escPressed) { _confirmQuit = false; } // keep playing
            base.Update(gameTime);
            return;
        }

        // In-game, Esc asks before leaving to the title menu.
        if ((escPressed || backPressed) && _dev.CanQuitOnEscape)
        {
            _confirmQuit = true;
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
        if (_confirmQuit) DrawQuitConfirm();

        _spriteBatch.End();
        CaptureIfRequested();
        base.Draw(gameTime);
    }

    private void DrawQuitConfirm()
    {
        _shapes.Fill(new Rectangle(0, 0, WindowWidth, WindowHeight), new Color(0, 0, 0, 180));

        int w = 460, h = 190;
        var panel = new Rectangle((WindowWidth - w) / 2, (WindowHeight - h) / 2, w, h);
        _shapes.Fill(panel, Palette.Panel);
        _shapes.Outline(panel, Palette.Grid, 2);

        int x = panel.X + 28;
        int y = panel.Y + 24;
        _spriteBatch.DrawString(_font, "Quit to menu?", new Vector2(x, y), Palette.Text, 0f,
            Vector2.Zero, 1.4f, SpriteEffects.None, 0f);
        y += 40;
        _spriteBatch.DrawString(_font, "This game will be abandoned.", new Vector2(x, y), Palette.TextMuted);
        y += 44;
        _spriteBatch.DrawString(_font, "Enter / Y   -   quit to menu", new Vector2(x, y), Palette.Crystite);
        y += 26;
        _spriteBatch.DrawString(_font, "Esc / N     -   keep playing", new Vector2(x, y), Palette.Food);
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
        // Background scene first: the landscape and the river run underneath, and the
        // plot squares are layered on top (river plots are left transparent so the
        // water shows through — as in the original).
        var board = new Rectangle(_layout.OriginX, _layout.OriginY,
            _layout.Cell * _layout.Width, _layout.Cell * _layout.Height);
        _shapes.Fill(board, BoardLand);
        DrawRiverBackground(board);
        DrawMountainBackground();

        foreach (var plot in _state.Map.AllPlots())
        {
            var rect = _layout.PlotRect(plot.X, plot.Y);

            // Plains and town are opaque; river and mountain squares stay clear so the
            // background scene shows through beneath the grid.
            if (!IsBackdrop(plot.Terrain))
                _shapes.Fill(rect, Palette.TerrainColor(plot.Terrain));

            if (plot.Terrain == Terrain.Town)
            {
                var label = "TOWN";
                var ls = _font.MeasureString(label);
                _spriteBatch.DrawString(_font, label,
                    new Vector2(rect.Center.X - ls.X / 2, rect.Center.Y - ls.Y / 2), Palette.Text);
            }

            // Every square gets a border, so the grid clearly sits over the scene.
            if (plot.IsOwned)
            {
                var owner = _state.PlayerById(plot.OwnerId);
                if (owner != null)
                    _shapes.Outline(rect, Palette.FromPacked(owner.Color), 3);
            }
            else
            {
                Color edge = plot.Terrain == Terrain.River ? WaterEdge
                           : IsMountain(plot.Terrain) ? MountainEdge : Palette.Grid;
                _shapes.Outline(rect, edge, 1);
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

    private static readonly Color BoardLand  = new(0x20, 0x27, 0x22);
    private static readonly Color WaterDeep  = new(0x1B, 0x3E, 0x4E);
    private static readonly Color WaterBank  = new(0x2C, 0x55, 0x66);
    private static readonly Color WaterGlint = new(0x8A, 0xC0, 0xD2);
    private static readonly Color WaterEdge  = new(0x35, 0x60, 0x72);
    private static readonly Color MountainGround = new(0x2B, 0x27, 0x22);
    private static readonly Color MountainRock   = new(0x52, 0x49, 0x3D);
    private static readonly Color MountainSnow   = new(0xCC, 0xD4, 0xDB);
    private static readonly Color MountainEdge   = new(0x47, 0x3F, 0x34);

    private static bool IsMountain(Terrain t) =>
        t is Terrain.Mountain1 or Terrain.Mountain2 or Terrain.Mountain3;

    private static bool IsBackdrop(Terrain t) => t == Terrain.River || IsMountain(t);

    /// <summary>
    /// Draws the mountain ranges as a background layer: each mountain plot gets rocky
    /// ground and a couple of peaks (one snow-capped), so adjacent plots read as a
    /// continuous range with the grid squares layered over them.
    /// </summary>
    private void DrawMountainBackground()
    {
        foreach (var plot in _state.Map.AllPlots())
        {
            if (!IsMountain(plot.Terrain)) continue;
            var cell = _layout.FullRect(plot.X, plot.Y);
            _shapes.Fill(cell, MountainGround);

            // Deterministic per-plot variation so peaks differ but never flicker.
            float v1 = ((plot.X * 7 + plot.Y * 13) % 5) / 5f;
            float v2 = ((plot.X * 11 + plot.Y * 5) % 5) / 5f;
            DrawPeak(cell, 0.32f, 0.36f + v1 * 0.16f, 0.62f, snow: false);
            DrawPeak(cell, 0.66f, 0.20f + v2 * 0.16f, 0.74f, snow: true);
        }
    }

    private void DrawPeak(Rectangle cell, float apexFracX, float apexFracY, float baseWidthFrac, bool snow)
    {
        float apexX = cell.Left + cell.Width * apexFracX;
        float apexY = cell.Top + cell.Height * apexFracY;
        float baseY = cell.Bottom;
        float baseW = cell.Width * baseWidthFrac;
        float snowLimit = apexY + cell.Height * 0.16f;

        for (int y = (int)apexY; y < baseY; y++)
        {
            float f = (y - apexY) / (baseY - apexY);
            float w = baseW * f;
            var color = (snow && y < snowLimit) ? MountainSnow : MountainRock;
            _shapes.Fill(apexX - w / 2f, y, w, 1, color);
        }
    }

    /// <summary>
    /// Draws a continuous, gently winding river down the town column as a background
    /// layer, with wavy banks and drifting surface glints — the plot grid is drawn
    /// over the top afterwards.
    /// </summary>
    private void DrawRiverBackground(Rectangle board)
    {
        float cell = _layout.Cell;
        int townCol = _state.Map.Width / 2;
        float centerX = _layout.OriginX + townCol * cell + cell / 2f;

        // The water body: horizontal slices whose center meanders and whose width
        // breathes, giving a natural riverbank rather than straight edges.
        for (int y = board.Top; y < board.Bottom; y += 2)
        {
            float ty = y - board.Top;
            float meander = MathF.Sin(ty * 0.016f + _time * 0.5f) * (cell * 0.10f);
            float half = cell * 0.44f + MathF.Sin(ty * 0.055f + _time * 0.8f) * (cell * 0.06f);
            float cx = centerX + meander;
            _shapes.Fill(cx - half - 2f, y, (half + 2f) * 2f, 2f, WaterBank); // lighter bank
            _shapes.Fill(cx - half, y, half * 2f, 2f, WaterDeep);             // deep water
        }

        // Surface glints: short dashes drifting along the current.
        for (int y = board.Top + 6; y < board.Bottom; y += 13)
        {
            float ty = y - board.Top;
            float meander = MathF.Sin(ty * 0.016f + _time * 0.5f) * (cell * 0.10f);
            float half = cell * 0.44f + MathF.Sin(ty * 0.055f + _time * 0.8f) * (cell * 0.06f);
            float cx = centerX + meander;
            for (float x = cx - half + 5; x < cx + half - 5; x += 11)
            {
                float wob = MathF.Sin(x * 0.2f + _time * 1.8f + ty * 0.1f) * 1.6f;
                float a = 0.4f + 0.35f * (0.5f + 0.5f * MathF.Sin(x * 0.12f - _time * 1.4f));
                _shapes.Fill(x, y + wob, 6f, 1.6f, WaterGlint * a);
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
