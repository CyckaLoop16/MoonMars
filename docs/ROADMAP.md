# Roadmap

The campaign's acts double as the development plan: **ship the Moon first**, then Mars,
then Beyond. Each milestone ends with something playable.

## M0: Foundations ✅
- Repo, design docs, tech decisions
- Pure-C# sim: hex math, seeded noise, procedural Mars map generator, tile yields
- Unity view: 3D terraced hex map, strategy camera, hover tile inspector
- Sim unit tests (`dotnet test`)

## M1: Clock & colony core (on the Moon)
- Tick engine: 1 tick = 1 day, pause plus 5 speeds, monthly economy pass
- Command queue (player and AI issue the same commands, applied on tick boundaries)
- Lunar map generator: south pole, shadowed ice craters, peaks of light
- Found a colony; place modules on hexes; power → ISRU → greenhouse chain
- Pops & jobs, life support, growth/decline
- HUD: time controls, resource bar, colony panel (UI Toolkit)

## M2: Pioneers
- Characters: roles, traits, skills, age, death
- Council seats and Administrators with modifiers
- Event system v1 (anomalies, character events)

## M3: Rivals I: the Treaty era
- 2 AI factions (utility AI using the same commands as the player)
- Territory claims, Influence, loyalty pressure, espionage and sabotage
- Research tree v1 with boosts; directive trees v1 for 2–3 factions

## M4: Act I vertical slice: "Luna" 🎯
- Earth relations: blocs, funding, cargo
- Act goal: self-sufficiency + lunar fuel depot
- Save/load
- **Playtest milestone: is the Moon fun start to finish?**

## M5: System view & Mars
- System map (Earth, Moon, Mars), transfer routes, launch windows
- Mars map (the M0 generator), dust storms
- Moon colonies become fuel and shipyard hubs
- All 5 factions

## M6: Conflict & independence
- The Treaty breaking; security units on hex map; supply = life-support range
- Independence mechanics, System Council
- Generational Pioneers (Mars-born traits)

## M7: Act III: Beyond
- Outposts on the system map (belt, Jovian/Saturnian moons)
- Endgame crisis
- All victory conditions

## M8: Presentation & balance
- URP, terrain shading, module models, VFX (dust storms, launches), audio
- Balance via headless AI-vs-AI runs
- UI polish, tutorial, onboarding
