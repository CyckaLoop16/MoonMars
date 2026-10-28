# Roadmap

Each milestone should end with something playable.

## M0: Foundations ✅ (this commit)
- Repo, design docs, tech decisions
- Pure-C# sim: hex math, seeded noise, procedural Mars map generator, tile yields
- Unity view: 3D terraced hex map, strategy camera, hover tile inspector, regenerate map
- Sim unit tests (`dotnet test`)

## M1: First colony & the turn loop
- Turn manager, `EndTurn` command
- Landing: choose a site, found a colony, claim surrounding hexes
- Pops, Life Support balance, growth/starvation
- Production queue with a few modules (Habitat, Solar Array, ISRU Plant, Greenhouse)
- Minimal UI Toolkit HUD: resource bar, colony panel, end-turn button

## M2: Modules, adjacency & logistics
- District-style module placement on hexes with adjacency bonuses
- Power grid and resource chains with cascading shortfalls
- Rovers and construction crews (units, movement costs, pathfinding)

## M3: Research & society
- Engineering tree and Society tree, data-driven
- Boosts triggered by gameplay ("Eurekas")
- Save/load, built on the command log

## M4: Earth
- Sponsors and charters (origins/ethics)
- Launch windows and cargo ordering
- Loyalty vs. Martian Identity pressure
- Event system with the first anomaly chains

## M5: Rivals
- AI colonies (utility-based AI using the same commands as the player)
- Diplomacy and Influence, Interplanetary Council
- Colonist factions and stability

## M6: Win conditions & presentation
- Victory conditions, end-game screen
- URP, real terrain shading, module models, dust storm VFX, audio
- Balance pass using headless AI-vs-AI runs

## M7: The Moon & beyond
- Lunar map preset and planet parameters
- Modding: load content from JSON
