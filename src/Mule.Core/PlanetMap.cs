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

        // Base: plains everywhere, with a river down the town column and the town itself.
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Terrain terrain =
                    (x == townColumn && y == height / 2) ? Terrain.Town :
                    (x == townColumn) ? Terrain.River :
                    Terrain.Plain;
                plots[x, y] = new Plot(x, y, terrain);
            }
        }

        // Scatter 2-3 mountain ranges — contiguous clusters of 3-4 plots, all rich in
        // Smithore (Mountain3 yields the most), so these regions reliably mine ore.
        int ranges = 2 + rng.Next(2); // 2 or 3
        for (int r = 0; r < ranges; r++)
            GrowMountainRange(plots, rng, width, height, size: 3 + rng.Next(2));

        return new PlanetMap(width, height, plots);
    }

    /// <summary>Grows one contiguous cluster of mountain plots out of plains.</summary>
    private static void GrowMountainRange(Plot[,] plots, Random rng, int width, int height, int size)
    {
        // Find a plains plot to seed the range.
        int sx = -1, sy = -1;
        for (int attempt = 0; attempt < 60 && sx < 0; attempt++)
        {
            int x = rng.Next(width), y = rng.Next(height);
            if (plots[x, y].Terrain == Terrain.Plain) { sx = x; sy = y; }
        }
        if (sx < 0) return;

        var cluster = new HashSet<(int, int)> { (sx, sy) };
        (int dx, int dy)[] dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        while (cluster.Count < size)
        {
            // Gather plains cells adjacent to the cluster.
            var candidates = new List<(int, int)>();
            foreach (var (cx, cy) in cluster)
                foreach (var (dx, dy) in dirs)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    if (plots[nx, ny].Terrain == Terrain.Plain && !cluster.Contains((nx, ny)))
                        candidates.Add((nx, ny));
                }
            if (candidates.Count == 0) break;
            cluster.Add(candidates[rng.Next(candidates.Count)]);
        }

        foreach (var (cx, cy) in cluster)
            plots[cx, cy] = new Plot(cx, cy, Terrain.Mountain3);
    }
}
