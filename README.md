# Waystation

*Working title.* A **spaceport management tycoon** in space, in the bright low-poly style of
**No Man's Sky**. You start with one floating landing platform and a fuel tank. Ships drop out of
the void, circle while they wait, land, refuel, pay and leave. Keep them happy, grow your
reputation, and expand with corridors, hangars and new landing pads.

- 📐 [Design](docs/DESIGN.md): core loop, economy, what comes next
- 🛠️ [Tech & art pipeline](docs/TECH.md): sim/view split, art drop-in slots
- 🗺️ [Roadmap](docs/ROADMAP.md)

> The previous project in this repo, **Regolith** (a Moon → Mars 4X), is archived on the
> [`regolith`](https://github.com/CyckaLoop16/MoonMars/tree/regolith) branch.

## Status: first playable prototype

- **Real-time with pause** and 1×/2×/4× speed
- **Ship flow:** inbound → holding pattern (patience runs down) → landing → refuel (→ repairs) → pay → depart
- **Shuttles** use landing pads; **haulers** need a hangar and often pay for repairs
- **Build:** landing pads, hangars, corridors (drag to lay; they auto-connect), demolish for a 50% refund
- **Connections:** modules must link to the starter pad (directly or through corridors) to get fuel
- **Economy:** fuel margins, landing and repair fees, upkeep, automatic tanker restocks, reputation-driven traffic
- **Visuals:** placeholders only: a holographic build grid over nebulae, stars, a ringed gas giant and
  drifting asteroids. Art is made separately and dropped in (see [TECH.md](docs/TECH.md#art-pipeline))

## Layout

```
Unity/                          Unity 6 project
  Assets/Waystation/Sim/        pure C# simulation (no UnityEngine; asmdef enforces it)
  Assets/Waystation/View/       rendering, camera, input, HUD
  Assets/Waystation/Resources/  runtime-loaded assets (shader; art drop-in folders)
Tests/Waystation.Sim.Tests/     NUnit tests for the sim, run with plain `dotnet test`
docs/                           design, tech, roadmap
```

## Getting started

### Run the game
1. Pull, then open the `Unity/` folder in Unity Hub (Unity 6).
2. Press **Play** in any scene (even an empty one). The game boots itself.

Controls: build with the toolbar or `P` pad · `H` hangar · `C` corridor · `X` demolish · `Esc` select ·
left-click place/select (drag for corridors) · `Space` pause · `1`/`2`/`3` speed · `WASD`/right-drag
pan · wheel zoom · `Q`/`E` rotate.

**First steps:** watch a few shuttles use the starter pad. When you can afford it, build a second
landing pad touching the first, or anywhere with a corridor linking it back. Save up for a hangar
to start taking haulers.

### Run the sim tests (no Unity needed)

```bash
dotnet test Tests/Waystation.Sim.Tests
```
