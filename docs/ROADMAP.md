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

## P1: Feel & feedback
- Playtest and tune the first 15 minutes
- Ship arrival/landing polish: thrusters, landing lights, refuelling hose, docking clamps
- Click a ship to follow it; sound effects
- Art drop-ins from GPT: pads, hangar, corridor pieces, shuttle, hauler

## P2: More to build
- **Air traffic control tower**: automatic landing clearance (flips `Station.AutoAssign`)
- Buildings placed on foundations
- Fuel tanks (capacity) and a refinery (cheaper fuel); player-set fuel price
- Small / medium / large pads and more ship classes
- Traveller services: lounge, diner, trade terminal

## P3: People and stories
- Pilots with needs and moods; VIPs and contracts
- Staff (fuel crews, mechanics) walking the corridors
- Events: convoys, solar storms, pirates

## Later
- Save/load (command log), goals and scenarios, planetside stations
