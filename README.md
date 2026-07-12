# M.U.L.E.

A modern desktop remake of the 1983 Commodore 64 classic *M.U.L.E.* — the
four-player economic strategy game of colonizing the planet Irata. Built in C#
with [MonoGame](https://monogame.net/), it runs as a native cross-platform
desktop application (Windows / macOS / Linux) — no browser required.

## Status

Early scaffolding. The foundation is in place and runnable:

- ✅ Deterministic planet map generation (terrain, rivers, mountains, town)
- ✅ Core simulation model — players, plots, MULEs, resources, market/prices
- ✅ Turn/phase state machine (LandGrant → Development → Production → Auction → Resolution)
- ✅ Rendered game board with ownership, MULEs, and live player scoring
- ⬜ Real-time development phase (movement, store, installing MULEs)
- ⬜ Production resolution and colony events
- ⬜ Real-time double-auction phase
- ⬜ AI opponents
- ⬜ Networked multiplayer

## Architecture

The code is split so the game logic never depends on the rendering engine — this
keeps the simulation testable and makes networked multiplayer feasible later.

| Project | Purpose |
|---------|---------|
| `src/Mule.Core` | Pure C# simulation. No MonoGame dependency. The authoritative game state a network layer can serialize and sync. |
| `src/Mule.Game` | MonoGame desktop app: rendering, input, and the game loop. References `Mule.Core`. |

## The game

Up to four colonists compete over a fixed number of months to build the most
wealth. Each turn they claim land, outfit MULEs to harvest one of four
resources, and haggle in a live auction:

- **Food** — buys you time each turn
- **Energy** — powers your plots' production
- **Smithore** — refined into new MULEs
- **Crystite** — a pure export good, price swings wildly

## Building & running

Requires the [.NET SDK](https://dotnet.microsoft.com/) (9.0+).

```sh
dotnet run --project src/Mule.Game
```

Press **Esc** to quit.

## License

Fan project for educational purposes. *M.U.L.E.* is a trademark of its
respective owners; this is an independent, non-commercial remake.
