using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Mule.Game;

/// <summary>Everything needed to start a game, chosen on the setup screen.</summary>
public struct GameConfig
{
    public int TotalPlayers;
    public int Humans;
    public int Months;
    public int Seed;
    public int StartMoney;
    public string DifficultyName;
}

/// <summary>
/// The pre-game configuration screen: pick player count, how many are human,
/// difficulty, game length and the map seed, then start. A simple keyboard menu —
/// Up/Down to move between rows, Left/Right to change a value, Enter to begin.
/// </summary>
public sealed class SetupScreen
{
    private enum Row { Players, Humans, Difficulty, Length, Seed, COUNT }

    private static readonly string[] DifficultyNames = { "Beginner", "Standard", "Tournament" };
    private static readonly int[] DifficultyMoney = { 1500, 1000, 600 };
    private static readonly int[] LengthOptions = { 6, 9, 12 };

    private int _row;
    private int _players = 4;
    private int _humans = 1;
    private int _difficulty = 1;   // Standard
    private int _lengthIndex = 2;  // 12 months
    private int _seed;

    private readonly Random _rng = new();
    private KeyboardState _prev;

    public SetupScreen()
    {
        _seed = _rng.Next(1, 9999);
    }

    /// <summary>Returns true when the player has confirmed and the game should start.</summary>
    public bool Update(KeyboardState keys)
    {
        bool start = false;

        if (Pressed(keys, Keys.Down)) _row = (_row + 1) % (int)Row.COUNT;
        if (Pressed(keys, Keys.Up)) _row = (_row - 1 + (int)Row.COUNT) % (int)Row.COUNT;

        int dir = 0;
        if (Pressed(keys, Keys.Right)) dir = 1;
        if (Pressed(keys, Keys.Left)) dir = -1;
        if (dir != 0) Adjust((Row)_row, dir);

        if (Pressed(keys, Keys.R)) _seed = _rng.Next(1, 9999);
        if (Pressed(keys, Keys.Enter) || Pressed(keys, Keys.Space)) start = true;

        _prev = keys;
        return start;
    }

    private void Adjust(Row row, int dir)
    {
        switch (row)
        {
            case Row.Players:
                _players = Math.Clamp(_players + dir, 2, 4);
                _humans = Math.Clamp(_humans, 1, _players);
                break;
            case Row.Humans:
                _humans = Math.Clamp(_humans + dir, 1, _players);
                break;
            case Row.Difficulty:
                _difficulty = (_difficulty + dir + DifficultyNames.Length) % DifficultyNames.Length;
                break;
            case Row.Length:
                _lengthIndex = (_lengthIndex + dir + LengthOptions.Length) % LengthOptions.Length;
                break;
            case Row.Seed:
                _seed = Math.Clamp(_seed + dir, 1, 9999);
                break;
        }
    }

    public GameConfig BuildConfig() => new()
    {
        TotalPlayers = _players,
        Humans = _humans,
        Months = LengthOptions[_lengthIndex],
        Seed = _seed,
        StartMoney = DifficultyMoney[_difficulty],
        DifficultyName = DifficultyNames[_difficulty],
    };

    private bool Pressed(KeyboardState keys, Keys key) => keys.IsKeyDown(key) && _prev.IsKeyUp(key);

    public void Draw(SpriteBatch batch, ShapeBatch shapes, SpriteFont font, int screenW, int screenH)
    {
        // Title block.
        var titleSize = font.MeasureString("M . U . L . E .") * 2.4f;
        batch.DrawString(font, "M . U . L . E .",
            new Vector2((screenW - titleSize.X) / 2, screenH * 0.14f), Palette.Text, 0f,
            Vector2.Zero, 2.4f, SpriteEffects.None, 0f);
        string subtitle = "Establish a New Colony";
        var subSize = font.MeasureString(subtitle);
        batch.DrawString(font, subtitle, new Vector2((screenW - subSize.X) / 2, screenH * 0.14f + titleSize.Y + 8),
            Palette.TextMuted);

        // Options panel.
        int w = 520, rowH = 52;
        int h = rowH * (int)Row.COUNT + 48;
        var panel = new Rectangle((screenW - w) / 2, (int)(screenH * 0.34f), w, h);
        shapes.Fill(panel, Palette.Panel);
        shapes.Outline(panel, Palette.Grid, 2);

        var cfg = BuildConfig();
        string[] labels =
        {
            "Players", "Humans", "Difficulty", "Game length", "Map seed"
        };
        string[] values =
        {
            _players.ToString(),
            $"{_humans} human{(_humans > 1 ? "s" : "")}  ({_players - _humans} AI)",
            $"{cfg.DifficultyName}  (${cfg.StartMoney} start)",
            $"{cfg.Months} months",
            _seed.ToString("D4"),
        };

        for (int i = 0; i < labels.Length; i++)
        {
            int y = panel.Y + 24 + i * rowH;
            bool sel = i == _row;
            var rowRect = new Rectangle(panel.X + 16, y - 6, w - 32, rowH - 8);
            if (sel) shapes.Fill(rowRect, Palette.PanelLight);

            batch.DrawString(font, labels[i], new Vector2(panel.X + 34, y + 6),
                sel ? Palette.Text : Palette.TextMuted);

            string val = sel ? $"<  {values[i]}  >" : values[i];
            var vs = font.MeasureString(val);
            batch.DrawString(font, val, new Vector2(panel.Right - 34 - vs.X, y + 6),
                sel ? Palette.Food : Palette.Text);
        }

        // Controls hint + start prompt.
        string hint = "Up/Down: choose row    Left/Right: change    R: random seed";
        var hs = font.MeasureString(hint);
        batch.DrawString(font, hint, new Vector2((screenW - hs.X) / 2, panel.Bottom + 28), Palette.TextMuted);

        string go = "Press Enter to start";
        var gs = font.MeasureString(go) * 1.2f;
        batch.DrawString(font, go, new Vector2((screenW - gs.X) / 2, panel.Bottom + 60), Palette.Food,
            0f, Vector2.Zero, 1.2f, SpriteEffects.None, 0f);
    }
}
