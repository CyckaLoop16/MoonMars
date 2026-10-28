# Regolith

*Working title.* A real-time-with-pause **grand strategy 4X with a city-builder core**,
somewhere between **Civilization VI**, **Hearts of Iron IV** and **Stellaris**.

Rival factions race from the **Moon → Mars → Beyond** over a ~20-hour campaign. Build colonies
hex by hex, outmaneuver rivals from cold war to open conflict, and lead through **Pioneers**:
named characters who age, scheme and become legends.

- 📐 [Design brainstorm](docs/DESIGN.md): pillars, mechanics, victory conditions
- 🛠️ [Tech stack & architecture](docs/TECH.md): why Unity, sim/view split
- 🗺️ [Roadmap](docs/ROADMAP.md): milestones M0 → M8 (Moon slice first)

## Status: M1 (clock & colony core) in progress

- **Act I map:** the real lunar south pole at 3 km per hex. Named landmarks (Shackleton, de Gerlache, Malapert…) sit at their true positions, with procedural craters between them. Four sizes, up to 168×110 hexes
- **Real-time with pause *or* monthly turns** (toggle with `T`; still deciding which). 1 tick = 1 day from Jan 2040, including the lunar day/night cycle
- **Land a base and build modules on hexes:** solar, fission reactor, ice mine, oxygen plant, greenhouse, foundry, habitat
- **Survival:** a life-support economy with power sharing, crew jobs, lethal shortages and crew arrivals from Earth
- **Visuals:** placeholders only. Art is produced separately and dropped in (see [TECH.md](docs/TECH.md#art-pipeline))
- (M0) A procedural Mars map generator, ready for Act II

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

Controls: `T` switch real-time / turns · real-time: `Space` pause, `1`–`5` speed · turns: `Enter`
end turn · left-click select · `WASD`/right-drag pan · `Q`/`E` rotate · wheel zoom · `R` new map.

**First steps:** click a tile next to a dark shadowed crater (ice) and ideally near a yellow-marked
peak of light, then land your base. Build power first, then the oxygen plant, then an ice mine on
the crater, then a greenhouse. Unpause, and watch out for the lunar night.

### Run the sim tests (no Unity needed)

```bash
dotnet test Tests/Regolith.Sim.Tests
```
