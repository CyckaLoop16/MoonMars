# Tech & Art Pipeline

## Stack

| Need | Choice |
|---|---|
| Engine | Unity 6 |
| Language | C# (the sim stays .NET Standard 2.1-compatible for Unity) |
| Input | Input System package (code also compiles under the legacy Input Manager) |
| UI | IMGUI placeholder HUD, then UI Toolkit for the real UI |
| Tests | NUnit via `dotnet test` (no Unity licence needed) |
| Version control | Git + Git LFS for binary art (see `.gitattributes`) |

## Architecture: simulation separate from presentation

- `Waystation.Sim` has **no UnityEngine references** (enforced by its asmdef). It holds the
  station, buildings, ships and all rules, and runs in fixed ticks (10 per game second).
- `Waystation.View` reads sim state and renders it, and turns input into **commands**
  (`PlaceBuilding`, `Demolish`). It never changes sim state directly.
- Real time is just "how many ticks per frame": pause = 0, speed ×1/×2/×4. The sim never reads
  the clock and all randomness comes from a seeded RNG, so runs are reproducible (useful for
  balancing, bug reports and save files via the command log).

## Art pipeline

Art is produced **outside the codebase** (GPT). Code ships placeholders, and every visual has a
drop-in slot. Put a prefab at the path and it replaces the placeholder with no code changes.

| Asset | Drop-in path | Conventions |
|---|---|---|
| Starter pad | `Unity/Assets/Waystation/Resources/Buildings/StarterPad.prefab` | Pivot at footprint centre, deck top at Y=0, 1 unit = 1 grid cell (3×3) |
| Landing pad | `.../Resources/Buildings/LandingPad.prefab` | 3×3 |
| Hangar | `.../Resources/Buildings/Hangar.prefab` | 4×4, open enough that a docked ship is visible |
| Walkway deck | `.../Resources/Buildings/Walkway_Deck.prefab` | Full 1×1 grated catwalk deck |
| Walkway railing | `.../Resources/Buildings/Walkway_Railing.prefab` | Railing along the cell's **+Z** edge; placed (rotated) on every side with no neighbour |
| Foundation | `.../Resources/Buildings/Foundation.prefab` | Full 1×1 heavy deck plate |
| Foundation edge | `.../Resources/Buildings/Foundation_Edge.prefab` | Trim along the **+Z** edge; placed on every open side |
| Shuttle | `.../Resources/Ships/Shuttle.prefab` | Nose along +Z, pivot at the bottom of the gear, ~1.5 units long |
| Hauler | `.../Resources/Ships/Hauler.prefab` | Nose along +Z, ~2.5 units long |

### Style brief for GPT (grim-dark)

- **Materials:** standard URP **Lit** materials (albedo, normal, metallic/roughness) with **emissive
  maps** for lamps, windows, screens and engine nozzles. No baked lighting in vertex colours.
  Emissive is what reads in the dark and what bloom picks up.
- **Palette:** gunmetal, rust, dirty hazard yellow, worn paint; glowing bits in sodium orange, cold
  blue-white or alarm red.
- **Attachment points** (empty child transforms; code adds lamps and FX there):

| Model | Child name | Used for |
|---|---|---|
| Ships | `FX_Engine` (any number, e.g. `FX_Engine_L`) | Exhaust plume; points backwards along its −Z |
| Ships | `FX_Damage` | Smoke and sparks while the ship needs repairs |

Lamps on modules are positioned in code (pad floodlights in the SW/NE corners at ~1 unit high,
status light at the NE corner, fuel pump on the south edge, hangar work lights in the back corners),
so match those spots with lamp heads in the models.

## Rendering, lighting and FX

- **URP is set up automatically** by `Assets/Waystation/Editor/UrpSetup.cs`: on first open it installs
  the Universal RP package, creates `Assets/Waystation/Settings/Waystation_URP.asset` (+ renderer),
  makes it the default pipeline, clears per-quality overrides and switches to Linear colour.
  Re-run any time via **Waystation → Set Up URP**. (Manual fallback: install *Universal RP*, Create →
  Rendering → URP Asset (with Universal Renderer), assign it in Project Settings → Graphics, set
  Player → Color Space → Linear.)
- **Post-processing** (`GrimPostFx`, URP only): ACES tonemapping, bloom, vignette, film grain,
  desaturated grade, teal/amber split toning, chromatic aberration. Built at runtime; no assets.
- **Lamps** (`FxLight` + `LightRig`): every lamp feeds the placeholder shader through global arrays
  (nearest 32) *and* drives a real Unity point light, so GPT's URP-lit assets are lit by the same
  lamps. Modes: steady, flicker (failing lamp), pulse, strobe (rotating beacon).
- **Placeholder shader** `Waystation/Lit Vertex Color`: baked form shading in vertex colours,
  station lamps added at runtime, vertex **alpha = emissive**. Works in Built-in and URP.
  `Waystation/Unlit Vertex Color` is used for the backdrop and grid.
- **Particles** (`Fx`): code-built, no textures (`Waystation/Particle Additive|Alpha`): exhaust,
  smoke, sparks, venting vapour, welding.
- The view watches sim state to trigger FX (refuelling, repairs, damage, landing burns); the sim
  itself knows nothing about FX.
