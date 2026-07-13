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
- ✅ Resource consumption — Food sets your turn length, Energy powers production
  (unpowered MULEs sit idle); the AI keeps itself self-sufficient in power
- ✅ Colony events — a monthly news bulletin (pirates, sunspots, pest attacks,
  meteor strikes, windfalls) that leans toward helping the trailer and troubling
  the leader, keeping the race close
- ✅ Setup screen — choose players, humans, difficulty, game length and map seed;
  play again from the end screen
- ✅ Smarter AI — stays energy- and food-self-sufficient and bids the auction with
  reservation prices (won't dump goods below the store price or overpay above it)
- ✅ Presentation polish — starfield + planet title screen, a looping chiptune title
  theme, auction trade-flash and floating gain effects, and procedural sound effects
  (all synthesized in code, no asset files)
- 🟡 Networked multiplayer — foundation in place: full-state snapshot serialization
  and a TCP transport, proven with a host→client loopback round-trip. Live gameplay
  sync is the next step.

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

- **Food** — how much you eat each month sets how long your next turn is
- **Energy** — each producing MULE burns one unit; unpowered MULEs sit idle
- **Smithore** — the store refines it into new MULEs
- **Crystite** — a pure export good, price swings wildly

## Building & running

Requires the [.NET SDK](https://dotnet.microsoft.com/) (9.0+).

```sh
dotnet run --project src/Mule.Game
```

On launch you land on the setup screen: pick the number of players, how many are
human, difficulty (which sets starting cash), game length, and the map seed, then
press Enter to start. Everything else defaults sensibly.

### Custom title music

The setup screen plays a built-in chiptune theme by default. To use your own track,
drop a 16-bit PCM WAV at `src/Mule.Game/music/title.wav` (or point `MULE_MUSIC` at
any WAV) and the game plays it instead. Convert other formats with ffmpeg, e.g.
`ffmpeg -i yourtune.flac -acodec pcm_s16le -ar 44100 src/Mule.Game/music/title.wav`.

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

## Building release artifacts

To produce distributable builds (a portable single-file exe and a Windows installer):

```powershell
pwsh scripts\publish.ps1
```

This writes:

- `publish\portable\MULE-Colony.exe` — a **single, self-contained, compressed
  executable**. No .NET install required; just run it.
- `publish\installer\MULE-Colony-Setup.exe` — a **Windows installer** (built with
  [Inno Setup 6](https://jrsoftware.org/isinfo.php)) with Start Menu and optional
  desktop shortcuts, installable without admin rights.

Both are self-contained (win-x64) and bundle the content and title music. Attach
them to a GitHub Release for sharing.

## License

Fan project for educational purposes. *M.U.L.E.* is a trademark of its
respective owners; this is an independent, non-commercial remake.
