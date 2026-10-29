# Roadmap

## P0: First playable ✅ (this commit)
- Sim: build grid, buildings, connections, ship flow, fuel logistics, upkeep, reputation-driven traffic
- View: space backdrop, isometric camera, placeholder modules and ships, placement ghost, corridor painting
- HUD: status bar, build toolbar, traffic board, module info, event log
- Sim tests (`dotnet test`)

## P0.5: Walkways, foundations, hailing ✅
- Open-air walkways (railings on open sides) and foundation platforms, both paintable
- Ships hail; the player clears each landing to a pad, or waves it off (until ATC)
- Pad numbers and status lights; richer pad and hangar placeholders; comms mast

## P0.75: Grim-dark look ✅
- Automatic URP setup; post-processing (bloom, vignette, grain, grade, chromatic aberration)
- Lamp system: sodium floods, failing flickers, beacons, work lights, emergency lamps; dark ambient
- FX: engine plumes and burns, damaged-ship smoke and sparks, refuel venting, welding sparks + arc light
- Grim backdrop (dying red star, dark planet, wrecks, drifting scrap), grim palette, amber terminal HUD
- [ ] Glitch on alerts; camera shake; audio (drones, klaxons, radio static)

## P0.9: Proper HUD ✅
- UI Toolkit HUD in code with a USS theme (amber terminal, scanlines)
- Top bar (credits trend, reputation, fuel, traffic, ATC mode, clock, speed), tabbed build bar with cards
- Comms panel with one-click pad clearance; inspector for buildings and ships; toasts; world labels and floating income
- Station log (L) with filters, ledger (M) with per-minute chart and books; pause menu (Esc)
- Sim: typed events (`EventKind`), ledger lines, hail-wait stats

## P1.0: Orbit, power and modules ✅
- Orbital day/night: the sun crosses the sky and sets behind the planet; a 2-minute eclipse every 6 minutes
- Power grid: radioisotope generator, solar arrays, battery banks, fuel cell generators, brownouts
- Modules built on foundations: solar, battery, generator, fuel tank, ice refinery, pilot lounge,
  comms array, traffic control (automatic clearance)
- Switch modules on and off; lounge services income; comms boosts traffic
- Space backdrop: big planet with day side, night lights, atmosphere and sunrise flare; moon;
  distant traffic; ice dust; build grid on a toggle (G)
- HUD: power and orbit gauges, power stats on build cards, module details in the inspector

## P1.1: Polish pass ✅
- HUD redesign: icon chips in the top bar, build dock with rendered thumbnails, compact comms and
  inspector, icon toasts; less text, no terminal scanlines
- Placement animation (drop, thump, dust, sparks, flash) and a dust puff on demolish
- Brighter scene: higher ambient, planetshine fill light, stronger sun, lighter grade

## P1.2: Ships, people and services ✅
- Ship roles (traveller, freighter, merchant, USM patrol, Guild explorer, executive) with needs and wishes
- People who walk out to service buildings, queue, visit, pay and walk back; ships wait for them
- Nine service buildings in Business, Leisure and Commerce; buildings that bring new kinds of traffic
- HUD: role icons and need/wish icons on comms cards, a "Wants" list in the ship inspector,
  visitors and earnings for service buildings

## P1.3: Real-world scale ✅
- 1 cell = 2 m: walkways 2 m wide, people 1.8 m, shuttles ~11 m, haulers ~17 m, pads 16 m, a 192 m build area
- Drag-to-fill painting (foundation rectangles, walkway L-runs) with cost preview
- Installed art auto-fitted to its slot; placeholders, lamps and FX scaled together

## Next (agreed)
- **Decks and trusses:** stack decks above and below, lifts between them, truss spines with modules
  mounted on any side; people path-find across decks; the camera shows one deck at a time
- **Docking arms** at the station's edge for freighters and warships (gangway to the station), and
  S/M/L pads for smaller craft

## P1: Feel & feedback
- Playtest and tune the first 15 minutes
- Ship arrival/landing polish: thrusters, landing lights, refuelling hose, docking clamps
- Click a ship to follow it; sound effects
- Art drop-ins from GPT: pads, hangar, corridor pieces, shuttle, hauler

## P2: More to build
- Player-set fuel price; ice deliveries for the refinery
- Heat: radiators as a second resource alongside power
- Small / medium / large pads and more ship classes
- Traveller services: lounge, diner, trade terminal

## P3: People and stories
- Pilots with needs and moods; VIPs and contracts
- Staff (fuel crews, mechanics) walking the corridors
- Events: convoys, solar storms, pirates

## Later
- Save/load (command log), goals and scenarios, planetside stations
