using System;
using Microsoft.Xna.Framework;
using Mule.Core;

namespace Mule.Game;

/// <summary>
/// Maps the planet grid to screen pixels. Computed once per frame from the map and
/// the on-screen map area, then shared by both rendering and the pawn controller so
/// they always agree on where each plot sits.
/// </summary>
public readonly struct MapLayout
{
    public readonly int OriginX;
    public readonly int OriginY;
    public readonly int Cell;
    public readonly int Width;
    public readonly int Height;

    public MapLayout(PlanetMap map, Rectangle area)
    {
        Width = map.Width;
        Height = map.Height;
        Cell = Math.Min(area.Width / map.Width, area.Height / map.Height);
        OriginX = area.X + (area.Width - Cell * map.Width) / 2;
        OriginY = area.Y + (area.Height - Cell * map.Height) / 2;
    }

    public Rectangle PlotRect(int x, int y) =>
        new(OriginX + x * Cell, OriginY + y * Cell, Cell - 2, Cell - 2);

    public Vector2 PlotCenter(int x, int y) =>
        new(OriginX + x * Cell + Cell / 2f, OriginY + y * Cell + Cell / 2f);

    public Rectangle Bounds =>
        new(OriginX, OriginY, Cell * Width, Cell * Height);

    /// <summary>Which tile a pixel falls in, or (-1,-1) if outside the grid.</summary>
    public (int X, int Y) TileAt(Vector2 pixel)
    {
        if (!Bounds.Contains(pixel)) return (-1, -1);
        int tx = (int)((pixel.X - OriginX) / Cell);
        int ty = (int)((pixel.Y - OriginY) / Cell);
        if (tx < 0 || ty < 0 || tx >= Width || ty >= Height) return (-1, -1);
        return (tx, ty);
    }
}
