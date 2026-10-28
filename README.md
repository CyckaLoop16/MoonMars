# Waystation

*Working title.* A **spaceport management tycoon** in space, in the bright low-poly style of
**No Man's Sky**. You start with one floating landing platform and a fuel tank. Ships drop out of
the void and **hail** the station. You clear each one to a pad, and it lands, refuels, pays and
leaves. Keep pilots happy, grow your reputation, and expand with walkways, foundations, hangars and
new landing pads.

- 📐 [Design](docs/DESIGN.md): core loop, economy, what comes next
- 🛠️ [Tech & art pipeline](docs/TECH.md): sim/view split, art drop-in slots
- 🗺️ [Roadmap](docs/ROADMAP.md)

> The previous project in this repo, **Regolith** (a Moon → Mars 4X), is archived on the
> [`regolith`](https://github.com/CyckaLoop16/MoonMars/tree/regolith) branch.

## Status: first playable prototype

- **Real-time with pause** and 1×/2×/4× speed
- **Ship flow:** inbound → **hailing** (patience runs down until you clear it to a pad) → landing → refuel (→ repairs) → pay → depart
- **Manual clearance** until you own air traffic control (coming later): click a hailing ship or use the Comms panel, or wave it off
- **Shuttles** use landing pads; **haulers** need a hangar and often pay for repairs
- **Build:** landing pads, hangars, open-air **walkways** with railings and **foundation** platforms (drag to
  paint; railings and trims join up automatically), demolish for a 50% refund
- **Connections:** modules must link to the starter pad (directly, or through walkways and foundations) to get fuel
- **Pads** show numbers and status lights (green free · amber cleared · red occupied · grey offline)
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

Controls: `T` or click a hailing ship → click a pad to clear it · build with the toolbar or `P` pad ·
`H` hangar · `C` walkway · `F` foundation · `X` demolish · `Esc` cancel · drag to paint walkways and
foundations · `Space` pause · `1`/`2`/`3` speed · `WASD`/right-drag pan · wheel zoom · `Q`/`E` rotate.

**First steps:** when a ship hails (blinking cyan beacon, Comms panel), clear it to Pad 1. When you
can afford it, build a second landing pad touching the first, or anywhere with a walkway linking it
back. Save up for a hangar to start taking haulers.

### Run the sim tests (no Unity needed)

```bash
dotnet test Tests/Waystation.Sim.Tests
```
