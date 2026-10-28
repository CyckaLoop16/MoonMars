# Regolith

*Working title.* A real-time-with-pause **grand strategy 4X with a city-builder core**,
somewhere between **Civilization VI**, **Hearts of Iron IV** and **Stellaris**.

Rival factions race from the **Moon → Mars → Beyond** over a ~20-hour campaign. Build colonies
hex by hex, outmaneuver rivals from cold war to open conflict, and lead through **Pioneers**:
named characters who age, scheme and become legends.

- 📐 [Design brainstorm](docs/DESIGN.md): pillars, mechanics, victory conditions
- 🛠️ [Tech stack & architecture](docs/TECH.md): why Unity, sim/view split
- 🗺️ [Roadmap](docs/ROADMAP.md): milestones M0 → M8 (Moon slice first)

## Status: M0 (foundations)

- Procedural Mars map: hemispheric dichotomy, polar caps, craters, volcanic rises with lava tubes,
  a Valles Marineris-style canyon, dune fields, plus ice and mineral deposits
- 3D terraced hex rendering, Civ-style strategy camera, hover tile inspector

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

Controls: `WASD` pan · `Q`/`E` rotate · mouse wheel zoom · right/middle-drag pan · `R` new map.

### Run the sim tests (no Unity needed)

```bash
dotnet test Tests/Regolith.Sim.Tests
```
