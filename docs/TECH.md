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
| Corridor centre | `.../Resources/Buildings/Corridor_Hub.prefab` | 1×1 cell centre piece |
| Corridor arm | `.../Resources/Buildings/Corridor_Arm.prefab` | Runs from cell centre to the **+Z** edge; rotated for each link |
| Shuttle | `.../Resources/Ships/Shuttle.prefab` | Nose along +Z, pivot at the bottom of the gear, ~1.5 units long |
| Hauler | `.../Resources/Ships/Hauler.prefab` | Nose along +Z, ~2.5 units long |

Rendering uses one unlit vertex-colour shader (`Waystation/Unlit Vertex Color`) with lighting
baked into vertex colours, so art and placeholders need no scene lights. GPT's earlier Regolith
art used the same convention.
