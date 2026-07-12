using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Mule.Game;

/// <summary>
/// Small helper for drawing filled and outlined rectangles from a single 1x1
/// white texture, so we can build the whole UI without image assets.
/// </summary>
public sealed class ShapeBatch
{
    private readonly SpriteBatch _batch;
    private readonly Texture2D _pixel;

    public ShapeBatch(GraphicsDevice device, SpriteBatch batch)
    {
        _batch = batch;
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
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
}
