# Regolith

*Working title.* A real-time-with-pause **grand strategy 4X with a city-builder core**,
somewhere between **Civilization VI**, **Hearts of Iron IV** and **Stellaris**.

Rival factions race from the **Moon → Mars → Beyond** over a ~20-hour campaign. Build colonies
hex by hex, outmaneuver rivals from cold war to open conflict, and lead through **Pioneers**:
named characters who age, scheme and become legends.

- 📐 [Design brainstorm](docs/DESIGN.md): pillars, mechanics, victory conditions
- 🛠️ [Tech stack & architecture](docs/TECH.md): why Unity, sim/view split
- 🗺️ [Roadmap](docs/ROADMAP.md): milestones M0 → M8 (Moon slice first)

## Status: Act I (Luna) taking shape. M1–M3 and M5 prototyped

- **Turn-based** (1 turn = 1 month) on a daily simulation, with a turn report
- **The real lunar south pole** at 3 km per hex: Shackleton, de Gerlache, Malapert and more at their true positions, with procedural craters between them. Four map sizes
- **Land a base and build modules on hexes:** solar, batteries, fission, ice mine, oxygen plant, greenhouse, foundry, lab, habitat
- **Survival:** a life-support economy with power, crew jobs, lethal shortages and the lunar day/night
- **Research:** Proven-tier tech tree with Civ-style boosts
- **Resources:** automatic territory yields, plus Civ-style deposits (ice, volatiles, ilmenite, rare earths, platinum)
- **Earth:** funding, cargo orders, sponsor objectives, mining and exporting strategic resources
- **Pioneers:** named characters with skills and traits, assigned to modules, colonies or the council. They walk around the map at their posts, and rare accidents can kill them
- **Visuals:** placeholders only. Art is produced separately and dropped in (see [TECH.md](docs/TECH.md#art-pipeline))

## Layout

```
Unity/                         Unity 6 project
  Assets/Regolith/Sim/         pure C# simulation (no UnityEngine; asmdef enforces it)
  Assets/Regolith/View/        Unity rendering, camera, input, UI
  Assets/Regolith/Resources/   runtime-loaded assets (shaders)
Tests/Regolith.Sim.Tests/      NUnit tests for the sim, run with plain `dotnet test`
docs/                          design + tech docs
```

## Getting started

### Run the game
1. Install **Unity 6** (LTS) through Unity Hub.
2. Unity Hub → **Add → Add project from disk** → select the `Unity/` folder.
   If Hub says the exact editor version isn't installed, pick any Unity 6 version you have.
3. On first open Unity generates the rest of `ProjectSettings/` and `.meta` files.
   **Commit those.** If it asks to enable the new Input System backend, click **Yes**.
4. Press **Play** in any scene (even the empty default one). The game boots itself.

Controls: `Enter` end turn · left-click select · `WASD`/right-drag pan · `Q`/`E` rotate ·
wheel zoom · `R` new map.

**First steps:** click a tile on Shackleton's sunlit rim (yellow peak markers, next to the dark
ice-filled crater) and land your base. Build a solar array on a peak, then an oxygen plant, an
ice mine in the crater (purple marker = volatiles, +50%), a greenhouse and a lab. Pick a research,
then end turns and watch the turn report through your first lunar nights.

### Run the sim tests (no Unity needed)

```bash
dotnet test Tests/Regolith.Sim.Tests
```
