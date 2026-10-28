# Waystation

*Working title.* A **grim-dark spaceport management tycoon**. A rusting waystation hangs in low
orbit over a scarred world, swinging into the planet's shadow every few minutes. You start with one
floating landing platform and a fuel tank. Ships
drop out of the dark and **hail** the station. You clear each one to a pad, and it lands, refuels, pays and
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
- **Manual clearance** until you own air traffic control (coming later): one click on a pad button in the Comms panel, click a hailing ship and then a pad, or wave it off
- **Shuttles** use landing pads; **haulers** need a hangar and often pay for repairs
- **Build:** landing pads, hangars, open-air **walkways** with railings and **foundation** platforms (drag to
  paint; railings and trims join up automatically), demolish for a 50% refund
- **Connections:** modules must link to the starter pad (directly, or through walkways and foundations) to get fuel
- **Orbit:** a 6-minute day/night cycle. The sun crosses the sky and sets behind the planet, and the
  station spends ~2 minutes of each orbit in the eclipse
- **Power:** solar arrays (sunlight only), battery banks, a backup fuel cell generator, brownouts that
  slow everything down; switch modules off to save power
- **Modules on foundations:** solar array, battery bank, fuel cell generator, fuel tank, ice refinery,
  comms array (more traffic), traffic control (automatic clearance)
- **Ships with needs and wishes:** travellers, freighters, merchants, USM patrols, Guild explorers and
  executives each want different things. Their people **walk out** to your **Business** (USM Barracks,
  Explorers' Guild, Corporate Office), **Leisure** (Pilot Lounge, Cantina, Observation Deck) and
  **Commerce** (Trading Outpost, Bazaar, Parts Depot) buildings, pay, and walk back before the ship leaves
- **Pads** show numbers and status lights (green free · amber cleared · red occupied · grey offline)
- **Economy:** fuel margins, landing and repair fees, upkeep, automatic tanker restocks, reputation-driven traffic
- **Grim-dark look:** URP (set up automatically), bloom/vignette/grade, pools of sodium light,
  failing lamps, red beacons, engine plumes, smoking damaged ships, welding sparks, venting fuel lines
- **HUD (UI Toolkit):** top bar, tabbed build bar, comms panel, inspector, toasts, labels over pads and
  ships, floating income, station log (`L`), ledger with charts (`M`), pause menu (`Esc`)
- **Visuals:** placeholder models only; art is made separately and dropped in (see [TECH.md](docs/TECH.md#art-pipeline))

## Layout

```
Unity/                          Unity 6 project
  Assets/Waystation/Sim/        pure C# simulation (no UnityEngine; asmdef enforces it)
  Assets/Waystation/View/       rendering, camera, input
  Assets/Waystation/View/UI/    HUD (UI Toolkit, built in code)
  Assets/Waystation/Resources/  runtime-loaded assets (shaders, UI styles, art drop-in folders)
Tests/Waystation.Sim.Tests/     NUnit tests for the sim, run with plain `dotnet test`
docs/                           design, tech, roadmap
```

## Getting started

### Run the game
1. Pull, then open the `Unity/` folder in Unity Hub (Unity 6).
2. On first open the project **installs URP and switches itself over** (watch the Console; it
   recompiles once). If anything fails, use **Waystation → Set Up URP** or the manual steps in
   [TECH.md](docs/TECH.md#rendering-lighting-and-fx).
3. Press **Play** in any scene (even an empty one). The game boots itself.

Controls: clear a hailing ship with its pad button in the **Comms** panel (right), or `T` / click the
ship, then click a pad · click a pad or ship to **inspect** it · build with the cards at the bottom or
`P` pad · `H` hangar · `C` walkway · `F` foundation · `X` demolish · drag to paint walkways and
foundations · `G` build grid · `L` log · `M` ledger · `Esc` closes a window, then the tool, then the selection, then
opens the menu · `Space` pause · `1`/`2`/`3` speed · `WASD`/right-drag pan · wheel zoom · `Q`/`E` rotate.

**First steps:** when a ship hails (blinking cyan beacon, Comms panel), clear it to Pad 1. When you
can afford it, build a second landing pad touching the first, or anywhere with a walkway linking it
back. Save up for a hangar to start taking haulers, but watch the **POWER** gauge first: a hangar
overloads the starter grid. Paint a 2×2 patch of foundations (`F`) next to the station, put a
**Solar Array** on it (Power tab), and add a **Battery Bank** so the lights stay on through the eclipse.

### Run the sim tests (no Unity needed)

```bash
dotnet test Tests/Waystation.Sim.Tests
```
