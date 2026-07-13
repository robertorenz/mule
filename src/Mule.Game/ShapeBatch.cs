using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Mule.Game;

/// <summary>
/// Small helper for drawing filled/outlined rectangles and circles from a couple of
/// generated textures, so we can build the whole UI without any image assets.
/// </summary>
public sealed class ShapeBatch
{
    private const int DiscSize = 128;

    private readonly SpriteBatch _batch;
    private readonly Texture2D _pixel;
    private readonly Texture2D _disc;

    public ShapeBatch(GraphicsDevice device, SpriteBatch batch)
    {
        _batch = batch;
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // A soft-edged white disc, generated once, scaled wherever a circle is drawn.
        _disc = new Texture2D(device, DiscSize, DiscSize);
        var data = new Color[DiscSize * DiscSize];
        float r = DiscSize / 2f;
        for (int y = 0; y < DiscSize; y++)
        {
            for (int x = 0; x < DiscSize; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float d = MathF.Sqrt(dx * dx + dy * dy) / r;
                float a = Math.Clamp(1f - (d - 0.94f) / 0.06f, 0f, 1f); // feathered edge
                // Premultiplied alpha: RGB scaled by alpha so it composites correctly
                // under MonoGame's default (premultiplied) AlphaBlend.
                data[y * DiscSize + x] = new Color(a, a, a, a);
            }
        }
        _disc.SetData(data);
    }

    public void Fill(Rectangle rect, Color color) => _batch.Draw(_pixel, rect, color);

    public void Fill(float x, float y, float w, float h, Color color) =>
        _batch.Draw(_pixel, new Rectangle((int)x, (int)y, (int)w, (int)h), color);

    public void Outline(Rectangle rect, Color color, int thickness = 1)
    {
        _batch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        _batch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        _batch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        _batch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    /// <summary>Draws a filled, soft-edged circle centered at (cx, cy).</summary>
    public void FillCircle(float cx, float cy, float radius, Color color)
    {
        var dest = new Rectangle((int)(cx - radius), (int)(cy - radius), (int)(radius * 2), (int)(radius * 2));
        _batch.Draw(_disc, dest, color);
    }
}
