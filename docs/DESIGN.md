# Regolith: Game Design Brainstorm

> Working title. A turn-based colonization 4X on Mars (and later the Moon).
> **Civilization VI** is the backbone, with **Stellaris**-style narrative and politics,
> and the resource chains and survival pressure of a **city builder**.

## Elevator pitch

You lead a sponsor's first landing on Mars. Every hex is hostile: thin air, radiation,
dust storms, and a supply line to Earth that only opens every 26 months. Grow a few
pressurized habitats into a spread-out Martian civilization. Rival colonies will compete
with you, colonist factions will argue, and Earth will want something in return. You
win by out-building, out-researching, or out-negotiating everyone, or by cutting the
cord with Earth entirely.

## Design pillars

1. **Survival is the early game, politics is the late game.** Turns 1–50: keep people
   breathing. After that: who are we, and who do we answer to?
2. **The map is the puzzle.** As in Civ6, *where* you build matters more than *what*.
   Ice, lava tubes, crater rims and canyon walls drive adjacency and layout.
3. **Earth is a character.** It's your patron, lifeline, and eventually your rival.
   Supply windows, demands, and loyalty pressure come from Earth.
4. **Stories emerge from systems.** Events (Stellaris anomalies) connect to real
   simulation state rather than random popups.

## What we take from each inspiration

| From | Mechanic | Our Mars version |
|---|---|---|
| Civ6 | Hex map, cities settle and claim tiles | **Colonies** claim hexes; founding requires a landing or an overland convoy |
| Civ6 | Districts with adjacency bonuses | **Modules**: Habitat, ISRU Plant, Greenhouse, Reactor, Spaceport, Lab, Foundry. Adjacency: Lab next to canyon (+science, exposed strata), Habitat in a lava tube (radiation shielding) |
| Civ6 | Tech tree + civics tree, Eurekas/Inspirations | **Engineering** tree + **Society** tree; boosts come from doing things ("survive a dust storm" boosts *Dust-Hardened Electronics*) |
| Civ6 | Loyalty pressure | **Earth Loyalty vs. Martian Identity**: a per-colony meter pushed by nearby colonies and Earth's influence |
| Civ6 | Great People | **Pioneers**: named engineers, scientists, and leaders recruited with points |
| Civ6 | Governors | **Administrators** assigned to colonies |
| Civ6 | World Congress | **Interplanetary Council**: sessions every N turns; Earth blocs and colonies vote |
| Stellaris | Origins and ethics | **Sponsor** (fictional national agency, megacorp, scientific consortium, religious commune) + **Charter** (starting ethics) |
| Stellaris | Pop factions | Colonist factions: *Loyalists*, *Martian Firsters*, *Technocrats*, *Corporatists*. Their approval drives stability |
| Stellaris | Anomalies & event chains | Survey sites: microfossils, buried glaciers, derelict probes, an aquifer that may or may not be real |
| City builders | Production chains & logistics | Power → ISRU (O₂, H₂O, fuel) → Greenhouse (food) → Pop growth. Breakdowns cascade |
| City builders | Disasters | Global dust storms (solar output drops), solar flares (stay indoors), micrometeorites, habitat breaches |

## Yields and resources

**Tile yields** (Civ-style, per turn):
- **Materials** (production): regolith, metals, construction
- **Energy**: solar is weak on Mars and drops during storms; nuclear is better
- **Water**: from ice; feeds O₂, fuel, and farming
- **Science**

**Colony-level stockpiles and meters:**
- **Life Support** (O₂ / food / water balance). The growth meter, like Civ's food. It
  goes negative with unpleasant results.
- **Credits**: Earth funding and trade
- **Cohesion**: culture, identity, and civics progress
- **Influence**: diplomacy with Earth blocs and rival colonies (Stellaris-ish)
- **Stability**: from faction approval, amenities, and life-support margin

## Core loop (one turn ≈ one month)

1. Review alerts (life-support deficits, events, Earth demands)
2. Move units: rovers, survey teams, construction crews
3. Set production per colony (modules, units, projects)
4. Choose research and society focus
5. End turn → simulation resolves yields, growth, events, AI

**Launch windows:** every ~26 turns, an Earth supply window opens. You order cargo
(colonists, machinery, rare goods) months in advance with Credits. Missing a window
hurts. Becoming independent of the windows is a major goal.

## Victory conditions

- **Independence**: declare and survive Earth's response with enough Cohesion and
  self-sufficiency
- **Terraforming** (science): raise pressure and temperature past thresholds with
  planet-scale projects
- **Economic**: become the inner system's hub for fuel and goods
- **Council**: win Interplanetary Council votes (diplomatic)
- **Consolidation** (domination-lite): absorb rival colonies through loyalty,
  buyouts, or force

## The Mars map

Procedural, loosely based on real Mars geography (already in the prototype):
- **Hemispheric dichotomy**: low, smooth north; high, heavily cratered south
- **Polar ice caps**: lots of water, brutal to live on
- **Craters**: floors trap ice, rims expose minerals
- **Volcanic rises** (Tharsis-like) with **lava tubes**, the best habitat sites
- **A great canyon** (Valles Marineris-like): science, water, and hard terrain
- **Dune fields**: low value, they bury solar panels

## The Moon (later)

Use the same systems with different planet parameters: no atmosphere (no dust storms,
worse micrometeorites), 14-day nights (energy storage puzzle), ice only in polar
shadowed craters, and **Earth is 3 days away**. Supply is easy, but Earth's loyalty
pressure is much stronger. The Moon could work as a tutorial or short campaign, or
as a second body in a later Stellaris-style system map.

## Open questions

- Turn-based (Civ) or pausable real-time (city builder)? **Current lean: turn-based.**
- How deep should colonist simulation go: abstract pops (Civ) or individuals (Rimworld)?
  **Lean: pops with named Pioneers on top.**
- Multiplayer? It affects architecture early. The sim is being built deterministic so
  the option stays open.
- Fictional or real-ish factions? **Lean: fictional sponsors inspired by real agencies.**
