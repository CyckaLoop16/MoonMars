# Tech Stack & Architecture

## Engine: Unity 6 (LTS)

| Option | Verdict |
|---|---|
| **Unity 6** | ✅ **Chosen.** C# suits a heavy simulation. Large strategy and city-builder lineage (Humankind, Old World, Endless Space 2, Cities: Skylines). Strong UI tooling (UI Toolkit), ScriptableObjects for data-driven content, huge asset store. The Personal license is free under the revenue threshold. |
| Unreal Engine 5 | Built for high-fidelity action. C++/Blueprints slow down iteration on systems-heavy, UI-heavy 4X. Nanite and Lumen don't help a top-down hex map much. |
| Godot 4 | Good, lighter alternative; rejected in favor of Unity. |

## Key architecture decision: simulation is separate from presentation

```
┌──────────────────────────┐        commands        ┌──────────────────────────┐
│  Regolith.View (Unity)   │ ─────────────────────▶ │  Regolith.Sim (pure C#)  │
│  meshes, camera, UI,     │                        │  map, colonies, yields,  │
│  input, audio, VFX       │ ◀───────────────────── │  tech, AI, turn resolve  │
└──────────────────────────┘     state / events     └──────────────────────────┘
```

- `Regolith.Sim` has **no `UnityEngine` references**, enforced by its asmdef
  (`noEngineReferences: true`). That gives us:
  - **Unit tests without Unity** (`dotnet test` in `Tests/`, also usable in CI with no Unity license)
  - **Deterministic turns**: seeded RNG and no frame-time dependence. This enables replays,
    reproducible bug reports, and lockstep multiplayer later.
  - **Headless AI simulation**: run thousands of AI-vs-AI turns to balance the game
- Player actions will be **commands** (`FoundColony`, `QueueBuild`, `SetResearch`…) applied by
  the sim. The UI and the AI issue the same commands, which also gives us save files and undo.
- The view only reads sim state and renders it. It never mutates the sim directly.

## Content pipeline

- **Now:** terrain definitions live in C# (`TerrainCatalog`) while the prototype is small.
- **Next:** move content (terrains, modules, techs, events) to data, either ScriptableObjects
  authored in Unity or JSON for modding friendliness. Leaning JSON plus a Unity
  importer, so modders don't need the editor.

## Rendering

- Prototype: a single procedural mesh of terraced hex prisms with vertex colors and a tiny
  unlit shader that works in both Built-in and URP. No art assets needed.
- Later: URP, a proper lit terrain shader (triplanar regolith textures), dust/atmosphere
  fog, instanced module models, and a Civ6-style tilted camera (already started).

## Tooling

| Need | Choice |
|---|---|
| Engine | Unity 6 LTS |
| Language | C# (Sim targets .NET Standard 2.1 for Unity compatibility) |
| Input | Unity Input System package (code also compiles under the legacy Input Manager) |
| UI | IMGUI for the debug overlay now, UI Toolkit for the real UI |
| Tests | NUnit: `dotnet test` for Sim, Unity Test Runner for view code |
| Version control | Git + Git LFS for binary art and audio (see `.gitattributes`) |
| CI (later) | GitHub Actions: `dotnet test` on every push, and GameCI for Unity builds |
