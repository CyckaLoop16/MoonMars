# Roadmap

The campaign's acts double as the development plan: **ship the Moon first**, then Mars,
then Beyond. Each milestone ends with something playable.

## M0: Foundations ✅
- Repo, design docs, tech decisions
- Pure-C# sim: hex math, seeded noise, procedural Mars map generator, tile yields
- Unity view: 3D terraced hex map, strategy camera, hover tile inspector
- Sim unit tests (`dotnet test`)

## M1: Clock & colony core (on the Moon) ✅
- Daily simulation, monthly turns (real-time was prototyped and rejected), turn report
- Real lunar south-pole map at 3 km/hex; sizes up to 168×110; chunked rendering
- Land a base; place modules on hexes; power → oxygen / water → food chains
- Crew and jobs, life support, lethal shortages, lunar day/night, crew arrivals

## M2: Research & resources ✅ (prototype)
- Proven-tier tech tree (7 techs) with Civ-style boosts; Research Lab; locked modules
- Battery Bank for the lunar night
- Automatic territory yields; Civ-style deposits (ice, volatiles, ilmenite, rare earths, platinum)
- [ ] Job priorities, cancel/demolish construction
- [ ] Playtest pass on the first 2 years

---

**Act I (Luna) vertical slice, in build order:**

## M3: Earth
- Credits: monthly funding from your sponsor
- Cargo orders: crew, materials, prefab modules (the Moon is 3 days away, so cargo arrives next turn)
- Earth objectives ("operate an ice mine by 2042") that raise funding: Act I's goals
- Mineral Extractor: mine strategic deposits and export them for Credits

## M4: Exploration & expansion
- Rovers as units: movement costs, fog of war
- Survey sites: LCROSS impact site (Cabeus), the Athena lander (Mons Mouton), anomalies (grounded)
- Outposts and a second colony; territory growth over time

## M5: Pioneers
- Named characters: roles, traits, skills, age; rare accidents and near misses
- Council seats and Administrators with modifiers
- Event system v1

## M6: Rivals (Treaty era)
- 2–3 AI factions using the same commands as the player
- Claims, Influence, loyalty pressure, espionage; contested ice and peaks of light

## M7: Act I finale 🎯
- Propellant production; lunar fuel depot / shipyard (the gateway to Mars)
- Save/load (built on the command log)
- **Playtest milestone: is the Moon fun from start to finish?**

---

## M8+: Mars (Act II), Beyond (Act III), presentation & balance
- System view, launch windows, the Mars map (M0 generator), conflict and independence
- Outposts beyond Mars, endgame crisis, victories
- URP, terrain shading, art drop-ins, audio; balance via headless AI-vs-AI campaigns
