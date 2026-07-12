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

    // Optional headless screenshot: set MULE_SCREENSHOT=<path> to capture one
    // frame to a PNG and exit. Used for automated visual verification.
    private readonly string? _screenshotPath = Environment.GetEnvironmentVariable("MULE_SCREENSHOT");
    private int _frame;

    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;
    private const int SidebarWidth = 340;
    private const int HeaderHeight = 64;
    private const int Margin = 24;

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

    protected override void Initialize()
    {
        // Deterministic seed for now; will be chosen at setup / shared over the network.
        _state = GameFactory.NewGame(seed: 1983, humanPlayers: 1, totalPlayers: 4, totalMonths: 12);
        SeedDemoBoard();
        base.Initialize();
    }

    /// <summary>
    /// Temporary: hand out a couple of plots and MULEs so the board renders with
    /// real ownership and production markers. Replaced by the LandGrant phase later.
    /// </summary>
    private void SeedDemoBoard()
    {
        var map = _state.Map;
        (int x, int y, MuleOutfit outfit)[] demo =
        {
            (3, 1, MuleOutfit.Food),
            (5, 1, MuleOutfit.Energy),
            (2, 3, MuleOutfit.Smithore),
            (6, 3, MuleOutfit.Crystite),
            (3, 2, MuleOutfit.None),
        };

        for (int i = 0; i < demo.Length; i++)
        {
            var (x, y, outfit) = demo[i];
            if (!map.InBounds(x, y)) continue;
            var plot = map.At(x, y);
            if (plot.Terrain == Terrain.Town) continue;
            plot.OwnerId = i % _state.Players.Count;
            plot.Mule = outfit;
        }
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _shapes = new ShapeBatch(GraphicsDevice, _spriteBatch);
        _font = Content.Load<SpriteFont>("UIFont");
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.Background);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        DrawHeader();
        DrawMap();
        DrawSidebar();
        DrawFooter();

        _spriteBatch.End();

        if (_screenshotPath != null && ++_frame == 3)
        {
            CaptureScreenshot(_screenshotPath);
            Exit();
        }

        base.Draw(gameTime);
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
        var map = _state.Map;
        int areaX = Margin;
        int areaY = HeaderHeight + Margin;
        int areaW = WindowWidth - SidebarWidth - Margin * 2;
        int areaH = WindowHeight - HeaderHeight - Margin * 2 - 32;

        int cell = Math.Min(areaW / map.Width, areaH / map.Height);
        int gridW = cell * map.Width;
        int gridH = cell * map.Height;
        int originX = areaX + (areaW - gridW) / 2;
        int originY = areaY + (areaH - gridH) / 2;

        foreach (var plot in map.AllPlots())
        {
            var rect = new Rectangle(originX + plot.X * cell, originY + plot.Y * cell, cell - 2, cell - 2);
            _shapes.Fill(rect, Palette.TerrainColor(plot.Terrain));

            if (plot.Terrain == Terrain.Town)
            {
                var label = "TOWN";
                var ls = _font.MeasureString(label);
                _spriteBatch.DrawString(_font, label,
                    new Vector2(rect.Center.X - ls.X / 2, rect.Center.Y - ls.Y / 2), Palette.Text);
            }

            // Ownership border in the owner's color.
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

            // Installed MULE indicator.
            if (plot.HasMule)
            {
                int d = Math.Max(8, cell / 5);
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

        foreach (var p in _state.Players)
        {
            var card = new Rectangle(cardX, y, cardW, cardH);
            _shapes.Fill(card, Palette.PanelLight);
            _shapes.Outline(card, Palette.Grid, 1);

            // Color swatch + name + AI tag.
            _shapes.Fill(new Rectangle(cardX + 12, y + 14, 14, 14), Palette.FromPacked(p.Color));
            _spriteBatch.DrawString(_font, p.Name, new Vector2(cardX + 34, y + 12), Palette.Text);
            if (p.IsAI)
                _spriteBatch.DrawString(_font, "AI", new Vector2(card.Right - 34, y + 12), Palette.TextMuted);

            _spriteBatch.DrawString(_font, $"${p.Money}", new Vector2(cardX + 12, y + 36), Palette.Text);
            _spriteBatch.DrawString(_font, $"Score {p.Score(_state)}",
                new Vector2(card.Right - 118, y + 36), Palette.TextMuted);

            // Resource stores as a compact colored row.
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
        string hint = "Esc: quit    |    Scaffolding build - map, players & economy model wired up";
        _spriteBatch.DrawString(_font, hint, new Vector2(Margin, WindowHeight - 28), Palette.TextMuted);
    }
}
