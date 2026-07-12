using System;
using System.Collections.Generic;

namespace Mule.Core;

/// <summary>
/// The planet surface: a grid of plots with a town column down the middle.
/// The classic M.U.L.E. board is 9 columns by 5 rows.
/// </summary>
public sealed class PlanetMap
{
    public const int DefaultWidth = 9;
    public const int DefaultHeight = 5;

    public int Width { get; }
    public int Height { get; }

    private readonly Plot[,] _plots;

    public PlanetMap(int width, int height, Plot[,] plots)
    {
        Width = width;
        Height = height;
        _plots = plots;
    }

    public Plot At(int x, int y) => _plots[x, y];

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public IEnumerable<Plot> AllPlots()
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                yield return _plots[x, y];
    }

    /// <summary>The town plot (there is exactly one).</summary>
    public Plot Town()
    {
        foreach (var plot in AllPlots())
            if (plot.Terrain == Terrain.Town)
                return plot;
        return _plots[Width / 2, Height / 2];
    }

    /// <summary>
    /// Generates a deterministic planet from a seed. Determinism matters: the same
    /// seed must produce the same map on every machine so networked players agree.
    /// </summary>
    public static PlanetMap Generate(int seed, int width = DefaultWidth, int height = DefaultHeight)
    {
        var rng = new Random(seed);
        var plots = new Plot[width, height];
        int townColumn = width / 2;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Terrain terrain;
                if (x == townColumn && y == height / 2)
                {
                    terrain = Terrain.Town;
                }
                else if (x == townColumn)
                {
                    // A river runs down the town column.
                    terrain = Terrain.River;
                }
                else
                {
                    // Mountains cluster toward the edges; plains dominate the middle.
                    int distanceFromCenter = Math.Abs(x - townColumn);
                    double mountainChance = 0.15 + 0.12 * distanceFromCenter;
                    if (rng.NextDouble() < mountainChance)
                    {
                        terrain = rng.Next(3) switch
                        {
                            0 => Terrain.Mountain1,
                            1 => Terrain.Mountain2,
                            _ => Terrain.Mountain3
                        };
                    }
                    else if (rng.NextDouble() < 0.12)
                    {
                        terrain = Terrain.River;
                    }
                    else
                    {
                        terrain = Terrain.Plain;
                    }
                }

                plots[x, y] = new Plot(x, y, terrain);
            }
        }

        return new PlanetMap(width, height, plots);
    }
}
