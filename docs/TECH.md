# Tech & Art Pipeline

## Stack

| Need | Choice |
|---|---|
| Engine | Unity 6 |
| Language | C# (the sim stays .NET Standard 2.1-compatible for Unity) |
| Input | Input System package (code also compiles under the legacy Input Manager) |
| UI | UI Toolkit, built in code, styled with USS |
| Tests | NUnit via `dotnet test` (no Unity licence needed) |
| Version control | Git + Git LFS for binary art (see `.gitattributes`) |

## Architecture: simulation separate from presentation

- `Waystation.Sim` has **no UnityEngine references** (enforced by its asmdef). It holds the
  station, buildings, ships and all rules, and runs in fixed ticks (10 per game second).
- `Waystation.View` reads sim state and renders it, and turns input into **commands**
  (`PlaceBuilding`, `Demolish`, `AssignLanding`, `WaveOff`). It never changes sim state directly.
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
| Modules | `.../Resources/Buildings/SolarArray.prefab`, `BatteryBank`, `FuelCellGenerator`, `FuelTank`, `IceRefinery`, `PilotLounge`, `CommsArray`, `AtcTower` | Footprint per DESIGN.md; base at Y=0 (they stand on foundations) |
| Moving parts | `.../Resources/Buildings/SolarArray_Wing.prefab`, `CommsArray_Dish.prefab`, `AtcTower_Radar.prefab` | Wing: panels face +Y (code turns it to the sun); dish faces +Z; radar spins about Y |
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
- **Day/night** (`SkyCycle`, from `Sim/Orbit.cs`): one directional light for the sun (also lights
  URP-lit art, with soft shadows), plus shader globals `_WS_SunDir` / `_WS_SunColor` for the
  placeholders. The sun rakes in orange from the left at sunrise, is high and white at noon and
  sets on the right; in the eclipse it's off. Ambient rises a little by day, floodlights
  (`FxLight.Flood`) dim in sunlight, and every lamp stutters during a brownout.
- **Sky** (`WorldRenderer`): camera-space layers with the `Waystation/Backdrop Fade` shader (vertex
  colour × `_Tint`, additive or alpha per material) for the sun, the planet's day side, night lights,
  atmosphere and limb flare, faded by sun exposure in `UpdateSky`. They render at queue 2600+:
  after the station, before particles.
- **Placeholder shader** `Waystation/Lit Vertex Color`: baked form shading in vertex colours, the
  sun and station lamps added at runtime, vertex **alpha = emissive**. Works in Built-in and URP.
  `Waystation/Unlit Vertex Color` is used for the backdrop and grid.
- **Particles** (`Fx`): code-built, no textures (`Waystation/Particle Additive|Alpha`): exhaust,
  smoke, sparks, venting vapour, welding.
- The view watches sim state to trigger FX (refuelling, repairs, damage, landing burns); the sim
  itself knows nothing about FX.

## UI (UI Toolkit)

The whole HUD is one runtime `UIDocument` built in code by `View/UI/UiRoot.cs`. No UXML, no scene
setup: `PanelSettings` are created at runtime (scale with screen size, 1920×1080 reference).

- **Styles:** `Resources/UI/Waystation.uss` (classes all start with `ws-`), loaded through the theme
  `Resources/UI/WaystationTheme.tss`, which imports Unity's default runtime theme first. Colour means
  something: green good, amber warning, red bad, cyan comms. Use `Ui.Tone(label, "ws-bad")` to switch.
- **Helpers:** `View/UI/Ui.cs`: `Ui.El`, `Ui.Text`, `Ui.Button`, `.AddTo(parent)`, `Ui.Show`,
  `Ui.Set` (only touches text that changed), `Ui.Money`, `Ui.Clock`, and a `Bar` progress bar.
- **Widgets** (one class each in `View/UI/`, each with a `Root` element and an `Update()` that UiRoot
  calls every frame):

| Widget | What it shows |
|---|---|
| `TopBar` | credits and per-minute trend, reputation, fuel and tanker ETA, traffic stats, ATC mode, Log/Ledger buttons, clock, speed |
| `BuildBar` | hint line (errors, tool help), Select/Demolish, a tab per `BuildCategory`, a card per buildable module |
| `CommsPanel` | a card per hailing ship (patience, needs, one-click pad buttons, pick on map, wave off) and the traffic board |
| `Inspector` | the selected building or ship, with actions (focus, demolish, clear, wave off) |
| `ToastFeed` | pop-ups for `EventKind` Hail / Warning / Alert / Construction and refused commands |
| `WorldLabels` | pad names over the map (green/red while clearing a ship), ship status tags, floating `+N cr` |
| `LogWindow` (L) | the full station log, filterable |
| `LedgerWindow` (M) | income/expense chart per minute and the books per `LedgerLine` |
| `PauseMenu` (Esc) | resume, new station, quit |

- **Events:** the sim tags every `Station.Report` with an `EventKind` (and a building id and amount for
  revenue), so the UI decides how to present it without parsing text.
- **Money:** every credit goes through `Station.Earn/Spend` with a `LedgerLine`, so the ledger always
  balances (`LedgerTests`).
- **Map clicks vs UI:** `UiRoot.IsPointerOver` picks the panel; anything pickable blocks the map.
  Purely decorative layers (world labels, toasts, scanlines) use `PickingMode.Ignore`.
- **Adding a screen:** write a class with `Root` + `Update()`, add it in `UiRoot.Init` at the right layer
  (or as a `UiWindow` for a centred window with a hotkey), style it in `Waystation.uss`.
