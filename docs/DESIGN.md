# Waystation: Design

> Working title. A **grim-dark** spaceport management tycoon: a rusting waystation in low orbit
> over a scarred world. Every few minutes the station swings into the planet's shadow and has to
> survive the dark on batteries. You begin as a lonely refuelling stop and grow into a grimy hub
> that desperate pilots depend on.

## Pillars

1. **Start tiny, grow organically.** One platform and one fuel tank, then a station that sprawls
   across the build grid as corridors snake out to new pads and hangars.
2. **Readable bustle.** Every ship is visible: arriving, circling, landing, refuelling, leaving.
   You should be able to *see* a bottleneck before the numbers tell you.
3. **Service is reputation, reputation is traffic.** Happy pilots bring more pilots; angry ones
   cost you. The loop is: serve well → more traffic → expand → serve well.
4. **Grim-dark industrial.** Darkness, failing lamps, rust, smoke and sparks. Light is scarce and
   precious; every pad is a pool of sodium light in the void. Readability comes from light and
   emissives, not bright colours.

## Core loop (prototype)

```
ship arrives → hails the station (patience ↓) → you clear it to a pad → landing → refuel → [repairs] → pays → departs
                       │ you wave it off → leaves (reputation −1)
                       │ patience runs out → leaves angry (reputation −4)
                       │ 6 ships already circling → diverted (lost revenue)
```

- **Hailing and clearance**: until the station has a powered **Traffic Control** tower, the
  player clears every landing by hand: one click on a pad button in the Comms panel, or click the
  ship, then a pad. That busywork at high traffic is deliberate: it's what makes the tower worth buying.

- **Shuttles**: small, frequent. Land on any pad or a hangar. Pay a landing fee plus fuel.
- **Haulers**: big. Need a hangar. Often (45%) need repairs: slow but lucrative.
- **Fuel**: bought in bulk by automatic tanker orders (1.8 cr/unit) and sold at the pump
  (3 cr/unit). Run dry and ships leave angry.
- **Connections**: the starter pad holds the fuel tank. Any module must touch the station or link
  to it through walkways or foundations; unconnected modules show a red beacon and can't serve ships.
- **Reputation (0–100)** drives arrival rate and the share of haulers.
- **Upkeep** is charged every minute; demolishing refunds 50%.

## Orbit: day and night

The station circles the planet every **6 minutes** and spends **35%** of each orbit in its shadow
(about 2 minutes of eclipse). Sunrise and sunset take ~10 seconds, as they do in low orbit. In
sunlight the station is lit by hard white sunlight (orange at the edges of the day); in the eclipse
it's lamps and emissives only. A new station starts in mid-morning, so the first sunset arrives
after ~3 minutes.

## Power

Power is in **kW**, battery storage in **MJ**.
- **Supply:** the starter pad's radioisotope generator (8 kW, always), **solar arrays** (10 kW
  each in full sun, nothing in shadow) and **fuel cell generators** (12 kW, backup only).
- **Demand:** pads 2 kW, hangars 6, the starter pad 2, and modules (refinery 14, ATC 5, comms 4,
  lounge 3). Walkways, foundations, tanks and batteries use nothing.
- **Batteries** (starter pad 0.4 MJ, battery bank 0.9 MJ each) absorb the surplus by day and cover
  the deficit at night.
- **Generators** start by themselves when the batteries drop below 20% while the station is short,
  burn station fuel (0.3/s each), and stop at 60%.
- **Refineries** are a deferrable load: they only run on spare power or a battery above 35%.
- **Brownout:** flat batteries plus more demand than supply means only part of the demand is met.
  Pumps, repairs and refineries slow down in proportion; below 50%, lounges, comms and traffic
  control shut down. Lamps stutter. One hangar on the starter grid is enough to cause one, so power
  is the first thing to plan when expanding.
- Any powered module can be **switched off** from the inspector (it still costs upkeep).

## Buildings (prototype)

| Building | Size | Cost | Upkeep/min | Role |
|---|---|---|---|---|
| Waystation Pad | 3×3 | (start) | 5 | Starter pad + 800-unit fuel tank, the network hub |
| Landing Pad | 3×3 | 900 | 8 | Shuttles |
| Hangar | 4×4 | 2,600 | 25 | Haulers and shuttles, repairs |
| Walkway | 1×1 | 25 | 0.3 | Open-air catwalk; railings only on open sides, so paths join up |
| Foundation | 1×1 | 60 | 0.5 | Heavy deck plate that extends the station; modules are built on top |

Modules must stand entirely on foundations (a foundation can't be removed from under one):

| Module | Size | Cost | Upkeep/min | Power | Role |
|---|---|---|---|---|---|
| Solar Array | 2×2 | 450 | 1 | +10 kW in sun | Sun-tracking wings; nothing in the eclipse |
| Battery Bank | 1×2 | 350 | 1 | stores 0.9 MJ | Carries the station through the eclipse |
| Fuel Cell Generator | 1×2 | 700 | 4 | +12 kW backup | Starts on low battery, burns 0.3 fuel/s |
| Fuel Tank | 2×2 | 500 | 2 | – | +600 fuel capacity |
| Ice Refinery | 2×3 | 2,200 | 18 | −14 kW | Makes 1 fuel/s from comet ice, only on spare power |
| Comms Array | 2×2 | 1,800 | 12 | −4 kW | ×1.25 traffic and +2 holding slots each (up to 3) |
| Traffic Control | 2×2 | 4,000 | 30 | −5 kW | Clears hailing ships to free pads automatically |

## Ships, people and services

Every ship has a **role**, and every role wants different things. When a ship docks, its crew and
passengers **walk out** over walkways, foundations and pads to the buildings they want, **queue** if
it's full, spend a while **inside** (paying per visit), and walk back. **The ship can't leave until
everyone is aboard**, so service buildings close to the pads turn ships around faster, and far-flung
or unreachable ones hold pads up or never get used.

- **Need:** what the ship came for. If there's nowhere to get it, the ship leaves **disappointed**
  (reputation −1.5); met, +0.4.
- **Wishes:** nice to have (0–2 per ship). Each one met is +0.2 reputation and the visit fee.
- Most roles only come once the station has the building they're after, so building services
  **opens new traffic**. Every kind of service on offer also brings a little more traffic overall.

| Role | Hull | Comes when | Needs | Wishes |
|---|---|---|---|---|
| Traveller | shuttle | always | – | rest, food, sightseeing, shopping (1–2) |
| Freighter | hauler | a hangar exists | trade | food, rest, parts |
| Merchant | shuttle | Trading Outpost | trade | shopping, food |
| USM Patrol | shuttle | USM Barracks | report to the USM | food, rest |
| Guild Explorer | shuttle | Explorers' Guild | file a survey | parts, shopping, rest |
| Executive | shuttle | Corporate Office | a business meeting | sightseeing, food |

Service buildings, all built on foundations, in three categories:

| Category | Building | Size | Cost | Upkeep | Power | Service | Capacity | Visit | Fee |
|---|---|---|---|---|---|---|---|---|---|
| Business | USM Barracks | 3×2 | 2,600 | 10 | −5 kW | report to the USM | 4 | 16 s | 45 |
| Business | Explorers' Guild | 2×2 | 2,000 | 10 | −3 kW | file a survey | 3 | 12 s | 55 |
| Business | Corporate Office | 2×3 | 2,400 | 12 | −4 kW | business meeting | 2 | 14 s | 90 |
| Leisure | Pilot Lounge | 2×2 | 1,000 | 8 | −3 kW | rest | 4 | 14 s | 18 |
| Leisure | Cantina | 2×2 | 900 | 7 | −3 kW | food & drink | 4 | 10 s | 22 |
| Leisure | Observation Deck | 3×2 | 1,300 | 6 | −2 kW | sightseeing | 6 | 12 s | 15 |
| Commerce | Trading Outpost | 3×3 | 2,200 | 14 | −4 kW | trade cargo | 3 | 14 s | 70 |
| Commerce | Bazaar | 2×2 | 1,100 | 7 | −2 kW | shopping | 5 | 10 s | 25 |
| Commerce | Parts Depot | 2×2 | 1,400 | 9 | −4 kW | spare parts | 2 | 8 s | 60 |

People walk only on walkways, bare foundations and pads (not through modules); they enter a building
from any walkable cell next to it. Below 50% power, services close. A building with people inside
can't be demolished.

## Next ideas (not built yet)

- **Fuel-price setting** and fuel quality; ice deliveries for the refinery
- **More modules:** shops, crew quarters, cargo/trade terminal, shipyard, radiators and heat
- **Pad sizes:** small/medium/large pads; more ship classes (explorers, fighters, freighter captains)
- **Services:** traveller lounge/diner, trade terminal, ship shop, crew quarters and staff
- **Pilot needs and moods:** VIP pilots, contracts, faction ships (NMS-style races)
- **Events:** pirate raids, solar storms, convoys
- **Going planetside** later: the same station rules on a planet surface

## Look and feel

Grim-dark industrial sci-fi (think *Alien*, *Dead Space*, *Blade Runner*, *Barotrauma*):
- **Palette:** near-black void, gunmetal and rust, dirty hazard yellow, sodium-orange lamps, cold
  blue work lights, alarm red; gold foil and blue solar cells on the newer modules.
- **Space:** a huge planet curving across the bottom of the view: a day side with oceans,
  rust-brown continents and cloud, and night-side lights. A thin blue atmosphere on the limb,
  flaring orange at sunrise and sunset. The sun crosses the sky and sets behind the planet. A moon
  whose crescent follows the sun, slowly wheeling stars, distant ships blinking past, ice dust
  glinting around the station.
- **Light:** by day, hard vacuum sunlight with a sharp terminator; in the eclipse almost
  everything is dark and pads, walkways and modules are pools of lamplight. Some lamps are failing
  and flicker; in a brownout they all stutter. Unconnected modules go dark except a red emergency lamp.
- **FX tied to gameplay:** damaged ships arrive trailing smoke and sparks; repairs throw welding
  sparks and arc flashes; refuelling vents frosty vapour; engines burn hard on landing and take-off.
- **Camera:** isometric orthographic, 90° turns; post-processing with bloom, heavy vignette, film
  grain, desaturated teal/amber grade, slight chromatic aberration.
- **UI:** a clean game HUD: dark translucent panels, one amber accent, icons and numbers before words,
  rendered building thumbnails in the build dock. Colour carries meaning (green good, amber warning,
  red bad, cyan comms).
- **Building feels physical:** new modules drop into place, thump down with a dust ring, sparks and
  a flash; demolished ones leave a puff of dust.

All art is produced separately (GPT) and dropped into fixed slots; code ships placeholders, lamps and FX.
