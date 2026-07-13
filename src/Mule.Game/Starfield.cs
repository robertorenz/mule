using System;
using Microsoft.Xna.Framework;

namespace Mule.Game;

/// <summary>
/// A quiet, twinkling starfield used as a backdrop on the title/setup screen. Star
/// positions are fixed (seeded once); only their brightness breathes over time.
/// </summary>
public sealed class Starfield
{
    private struct Star
    {
        public float X, Y;   // normalized 0..1
        public float Size;
        public float Phase;  // twinkle offset
        public float Speed;
    }

    private readonly Star[] _stars;
    private float _time;

    public Starfield(int count = 140)
    {
        var rng = new Random(20250712);
        _stars = new Star[count];
        for (int i = 0; i < count; i++)
        {
            _stars[i] = new Star
            {
                X = (float)rng.NextDouble(),
                Y = (float)rng.NextDouble(),
                Size = 1f + (float)rng.NextDouble() * 2.2f,
                Phase = (float)rng.NextDouble() * MathF.PI * 2f,
                Speed = 0.6f + (float)rng.NextDouble() * 1.8f,
            };
        }
    }

    public void Update(float dt) => _time += dt;

    public void Draw(ShapeBatch shapes, Rectangle area)
    {
        foreach (var s in _stars)
        {
            float twinkle = 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(_time * s.Speed + s.Phase));
            var color = new Color(0.75f, 0.82f, 0.95f) * twinkle;
            float px = area.X + s.X * area.Width;
            float py = area.Y + s.Y * area.Height;
            shapes.Fill(px, py, s.Size, s.Size, color);
        }
    }
}
