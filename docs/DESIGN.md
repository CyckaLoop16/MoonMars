# Regolith: Game Design

> Working title. A real-time-with-pause **grand strategy 4X with a city-builder core**.
> Rival factions race from the **Moon → Mars → Beyond** over a ~20-hour campaign.
> The people you recruit, promote and lose along the way carry your civilization forward.

## The formula

The game sits between **Civilization VI**, **Hearts of Iron IV** and **Stellaris**, with
**city-builder** colonies at its core.

| From | We take | Regolith version |
|---|---|---|
| **Civ6** | Hex maps, districts and adjacency, visible tech tree with boosts, multiple victories, loyalty pressure | Each world is a hex map. Colonies spread over hexes as **modules**; placement and adjacency matter |
| **HOI4** | National focus trees, advisors and cabinet, production lines, supply-limited operations | **Directive trees** unique to each faction. A **council** of characters with modifiers. Units can't operate beyond **life-support supply range** |
| **Stellaris** | Real-time with pause, pops and jobs, leaders, factions and ethics, anomalies and events, system map, endgame crisis | Pops work jobs created by modules. **Pioneers** (characters) lead. A system map connects worlds. A late-game crisis tests everyone |
| **City builders** | Production chains, logistics, disasters, watching a settlement grow | Zoom into a colony and you're building it: power, oxygen, water, food, housing, morale |

## Pillars

1. **One map, three zoom levels.** System view (Stellaris) → world map (Civ) → colony
   (city builder). They're zoom levels of one game, never separate screens or minigames.
2. **People make history.** Abstract pops are the masses. Named Pioneers are the people who
   matter: they age, have ambitions, defect, die, and become legends.
3. **Space is the enemy, rivals are the threat.** Early on you fight the vacuum. Later you
   fight each other, and the vacuum punishes anyone who forgets it.
4. **The campaign is a journey.** Each act changes the game. The Moon teaches you, Mars
   tests you, and Beyond asks what your civilization has become.

## Time & pacing

- **Real-time with pause**, 5 speeds. 1 tick = 1 day. The economy resolves monthly.
- The campaign runs **~2040 → ~2160**. That's about three generations of Pioneers, so the
  founders you start with are retired or dead by the end.
- Target: **~20 hours** for a full campaign. Act I ≈ 4h, Act II ≈ 10h, Act III ≈ 5h.

## Campaign: three acts, one save

There's no hard reset between acts. Your Moon colonies keep running when Mars opens; their
role changes from home to supply base.

### Act I: Luna (2040–2065) · the foothold
- Small hex map: the south polar region, with ice in permanently shadowed craters and
  "peaks of eternal light" for solar power.
- Earth is **3 days away**: supply is easy, but Earth's control is strongest here.
- 2–3 rival factions land within sight of each other. Ice deposits are few and contested.
- **Act goal:** become self-sufficient and build the first **lunar fuel depot / shipyard**.
  It's the gateway to Mars.

### Act II: Mars (2065–2120) · the main act
- Large hex map (the M0 generator): ice caps, craters, lava tubes, the great canyon.
- Earth is **months away**, and supply only arrives in **26-month launch windows**.
- All factions race for Mars. Moon colonies become **fuel and shipbuilding hubs**, so
  losing your Moon base starves your Mars expansion.
- Colonist identity drifts toward **Martian**. Independence becomes thinkable.
- **Act goal:** dominate Mars by population, territory, influence or independence.

### Act III: Beyond (2120–2160) · the endgame
- Played mostly on the **system map**: asteroid belt mining, Jupiter's and Saturn's moons,
  and orbital habitats as **outposts** (abstract, not full hex maps, to cap scope).
- The **endgame crisis** hits (see below).
- Victory conditions resolve.

## Three zoom levels

| Zoom | You see | You do |
|---|---|---|
| **System** | Earth, Moon, Mars, belt, outer moons; transfer routes; launch windows | Interplanetary logistics, outposts, diplomacy, ship routes |
| **World** | Hex map of one body: territory, colonies, units, resources | Expand, claim, compete, move units, place colonies |
| **Colony** | The same hexes up close: modules, pipes and power lines, rovers, domes | Build, chain production, manage pops and jobs, respond to disasters |

## The colony layer (city builder)

- **Modules on hexes** (Civ6 districts): Habitat, Solar/Fission Power, ISRU Plant, Greenhouse,
  Lab, Foundry, Spaceport, Fuel Depot, Mass Driver…
- **Adjacency**: a habitat in a lava tube gets radiation shielding; a lab beside the canyon
  gets exposed strata (+science); a greenhouse beside an ISRU plant shares water.
- **Chains**: Power → ISRU (O₂, H₂O, propellant) → Greenhouse (food) → Pops. A failure in one
  link spreads down the chain, which is the city-builder tension.
- **Pops & jobs** (Stellaris): modules create jobs, pops fill them, unemployed pops get angry,
  and missing jobs means nothing gets produced.
- **Disasters**: dust storms (Mars), solar flares, micrometeorites, breaches, the 14-day
  lunar night.
- **Logistics between colonies**: rover routes, pipelines, power links. A colony cut off
  from its network is in trouble.

## The people layer: Pops and Pioneers

**Pops** are the population. Each has a **faction leaning** (Loyalist, Martian-first,
Technocrat, Corporatist…) and **needs**. Their mood drives stability.

**Pioneers** are named characters drawn from your pops or sent from Earth:

| Role | Inspired by | Does |
|---|---|---|
| **Administrator** | Civ6 governor, Stellaris governor | Runs a colony, with bonuses by trait |
| **Lead Scientist** | Stellaris scientist | Leads a research field; surveys anomalies |
| **Chief Engineer** | — | Speeds megaprojects; reduces disaster damage |
| **Expedition Leader** | Stellaris scientist / HOI4 general | Leads survey teams and units in the field |
| **Envoy** | — | Diplomacy with rivals and Earth blocs; espionage |
| **Council seats** | HOI4 advisors / cabinet | Faction-wide modifiers; your government |

- **Traits** are gained by doing things ("Survived a Breach", "Dust-Storm Veteran", "Ambitious").
- **Age and legacy**: Pioneers age and die. The greats become **Legends** with permanent
  bonuses, monuments and names on the map.
- **Ambitions and loyalty**: a brilliant but ambitious Pioneer may demand a council seat,
  lead a faction, or **defect to a rival** and take their secrets along.
- **Generations**: Act III's leaders were born on Mars. They have different traits, and
  they don't care about Earth.

## The strategic layer

### Factions & directive trees (HOI4 focus trees)
Each faction has a **unique directive tree** that gives it identity and story. Example branches:
*Earth Loyalty* vs. *Path to Sovereignty*, *Corporate Charter*, *Science Above All*, *Fortress Colony*.

### Research (Civ6 tree + Stellaris leaders)
- A visible tree across **Engineering / Life Sciences / Society**
- **Boosts** from gameplay ("Survive a dust storm" boosts *Hardened Electronics*)
- A Pioneer leading each field adds speed and trait bonuses
- Act transitions are gated by research (e.g. *Interplanetary Transit* opens Mars)

### Earth
- Earth isn't a player but a set of **blocs** with demands, funding and politics
- Launch windows, cargo orders, funding tied to delivering results
- **Loyalty vs. Identity** meters per colony (Civ6 loyalty), pushed by neighbours,
  Earth's distance and events

### Rivals & conflict: from cold war to hot war
Conflict escalates over the campaign:

1. **Treaty era** (Act I to mid Act II). Space law forbids open war, so you compete through
   claims, influence, trade embargoes, espionage, sabotage, poaching Pioneers, buying out
   colonies, and flipping them via loyalty pressure.
2. **The Treaty breaks**, through an event, a declaration of independence, or someone
   deciding to break it first. Open conflict follows: security forces, drones and orbital
   assets on the hex map, HOI4-lite.
3. **Supply is oxygen.** Units can only operate inside life-support range of a friendly
   colony or supply convoy. Logistics *is* the war.

### Diplomacy
Influence as currency (Stellaris), treaties, trade deals, shared research, non-aggression
pacts, joint independence, and a **System Council** that votes on resolutions (Civ6 World Congress).

## Faction roster (fictional, inspired by real-world blocs)

| Faction | Flavor | Strength | Weakness |
|---|---|---|---|
| **Atlantic Accord** | Democratic coalition program | Big Earth funding, stability | Slow, bureaucratic directives |
| **Tianlu Directorate** | State-run program | Fast construction, central planning | Low pop happiness, rigid |
| **Helion Dynamics** | Megacorp | Trade, Credits, buys what it needs | Low cohesion; Pioneers defect for pay |
| **Prometheus Consortium** | International science body | Research, terraforming | Weak security, depends on donors |
| **The Long Voyage** | Ideological settler movement | Growth, cohesion, hardship tolerance | Little Earth support |

Independents and breakaway colonies can emerge mid-game when a rival's colony revolts.

## Victory conditions

- **Sovereignty**: be the recognized independent power with the largest share of the
  system's population (political)
- **Terraforming**: bring Mars past habitability thresholds (science, Mars-focused)
- **Hegemony**: control most of the system's fuel and trade (economic)
- **Starship**: launch the first **interstellar ark** (the true "Beyond", Civ's space race)
- **Supremacy**: control or vassalize every rival's capital (domination)
- **Score** at campaign end date

## Endgame crisis (Stellaris-style), candidates

- **The Silence**: Earth goes dark (war or collapse). Supply stops. Only self-sufficient
  factions survive, and everyone must decide what they are without Earth.
- **Kessler Cascade**: orbital debris chain reaction. Launches become deadly, and the
  system map fragments.
- **The Signal**: something under Europa's ice answers back.

## Resources

- **Yields on tiles**: Materials, Energy, Water, Science
- **Colony stockpiles**: O₂, Food, Water, Propellant
- **Faction-wide**: Credits (Earth funding and trade), Influence (diplomacy, claims),
  Cohesion (identity, civics/directives progress), Research
- **Strategic**: Helium-3 (Moon), rare metals (belt), Pioneers themselves

## Scope strategy

This is a big game, so we build it as **vertical slices**:
1. **Act I: Luna** is the first complete, playable slice: city builder, Pioneers, 2 rivals,
   Treaty-era competition, research, directives. If the Moon is fun, the rest will be too.
2. **Act II: Mars** adds the system view, launch windows, independence and open conflict.
3. **Act III: Beyond** adds outposts, the crisis and full victories.

## Open questions

- Tone: grounded hard sci-fi throughout, or a hint of mystery (The Signal)?
- How many factions per game: 3 on the Moon, all 5 by Mars?
- Can you lose a Pioneer permanently in the field (permadeath) or only through events?
- Multiplayer later? The sim stays deterministic so the door stays open.
