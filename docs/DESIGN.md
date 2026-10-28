# Waystation: Design

> Working title. A **grim-dark** spaceport management tycoon: a rusting waystation hanging in a cold
> void beside a dying red star. You begin as a lonely refuelling stop and grow into a grimy hub
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

- **Hailing and clearance**: until the station buys an **air traffic control tower** (later), the
  player clears every landing by hand: click the ship (or use the Comms panel), then a pad. That
  busywork at high traffic is deliberate: it's what makes the ATC tower worth buying.

- **Shuttles**: small, frequent. Land on any pad or a hangar. Pay a landing fee plus fuel.
- **Haulers**: big. Need a hangar. Often (45%) need repairs: slow but lucrative.
- **Fuel**: bought in bulk by automatic tanker orders (1.8 cr/unit) and sold at the pump
  (3 cr/unit). Run dry and ships leave angry.
- **Connections**: the starter pad holds the fuel tank. Any module must touch the station or link
  to it through walkways or foundations; unconnected modules show a red beacon and can't serve ships.
- **Reputation (0–100)** drives arrival rate and the share of haulers.
- **Upkeep** is charged every minute; demolishing refunds 50%.

## Buildings (prototype)

| Building | Size | Cost | Upkeep/min | Role |
|---|---|---|---|---|
| Waystation Pad | 3×3 | (start) | 5 | Starter pad + 800-unit fuel tank, the network hub |
| Landing Pad | 3×3 | 900 | 8 | Shuttles |
| Hangar | 4×4 | 2,600 | 25 | Haulers and shuttles, repairs |
| Walkway | 1×1 | 25 | 0.3 | Open-air catwalk; railings only on open sides, so paths join up |
| Foundation | 1×1 | 60 | 0.5 | Heavy deck plate that extends the station; future buildings go on these |

## Next ideas (not built yet)

- **Air traffic control tower**: automatic clearance (the sim already supports it via `AutoAssign`)
- **Buildings on foundations**: fuel tanks, lounges, shops
- **Fuel tanks and refinery:** more storage, cheaper fuel, fuel-price setting
- **Pad sizes:** small/medium/large pads; more ship classes (explorers, fighters, freighter captains)
- **Services:** traveller lounge/diner, trade terminal, ship shop, crew quarters and staff
- **Pilot needs and moods:** VIP pilots, contracts, faction ships (NMS-style races)
- **Events:** pirate raids, solar storms, convoys
- **Going planetside** later: the same station rules on a planet surface

## Look and feel

Grim-dark industrial sci-fi (think *Alien*, *Dead Space*, *Blade Runner*, *Barotrauma*):
- **Palette:** near-black void, gunmetal and rust, dirty hazard yellow, sodium-orange lamps, cold
  blue work lights, alarm red. A dying red giant and a dark planet with a thin lit crescent.
- **Light:** almost everything is dark; pads, walkways and hangars are pools of lamplight. Some
  lamps are failing and flicker. Unpowered (unconnected) modules go dark except a red emergency lamp.
- **FX tied to gameplay:** damaged ships arrive trailing smoke and sparks; repairs throw welding
  sparks and arc flashes; refuelling vents frosty vapour; engines burn hard on landing and take-off.
- **Camera:** isometric orthographic, 90° turns; post-processing with bloom, heavy vignette, film
  grain, desaturated teal/amber grade, slight chromatic aberration.
- **UI:** old amber terminal (scanlines and glitches to come).

All art is produced separately (GPT) and dropped into fixed slots; code ships placeholders, lamps and FX.
