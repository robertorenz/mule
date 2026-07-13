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
- ✅ Real-time Development phase — walk the colonist, claim land, town store, install MULEs
- ✅ Production resolution and month/turn loop with end-game standings
- ✅ AI opponents — evaluate the best plot/resource, claim, buy, and install MULEs
- ✅ Real-time double-auction phase — converging bid/ask lines, store market-maker,
  Smithore refined back into MULEs
- ⬜ Colony events (pirates, sunspots, pest attacks)
- ⬜ Resource consumption (Food buys turn time, Energy powers production)
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

### Controls

| Key | Action |
|-----|--------|
| WASD / Arrows | Walk your colonist |
| Space | Context action: claim empty land · enter the town store · install a led MULE |
| Enter | End your turn (or continue at the month summary) |
| Esc | Quit |

The turn loop: claim a plot, walk to **TOWN** to buy and outfit a MULE, then walk
it back to your land and install it. When the timer runs out (or you press Enter),
production resolves and each resource goes to a **real-time auction** — raise or
lower your price with Up/Down; a trade fires the moment a buyer's line meets a
seller's, with the store bounding the market and refining Smithore into new MULEs.
Then the next month begins. The other three colonists are played by the AI, which
you can watch move, buy, build, and trade.

### Headless self-check

To play a full 12-month all-AI game in the console (no window), useful for
verifying the simulation logic:

```sh
MULE_SIMULATE=1 dotnet run --project src/Mule.Game
```

## License

Fan project for educational purposes. *M.U.L.E.* is a trademark of its
respective owners; this is an independent, non-commercial remake.
