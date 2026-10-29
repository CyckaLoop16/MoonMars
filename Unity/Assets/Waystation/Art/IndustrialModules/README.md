# Industrial module kit

Native URP Lit replacements for SolarArray, BatteryBank, FuelCellGenerator, FuelTank, IceRefinery, PilotLounge, CommsArray, AtcTower and Hangar. Shared worn-metal, ivory, olive and oxide-red materials match the pads and structural kit. New materials supply photovoltaic cells, illuminated glass, screens and operating lights. Geometry has normals, UVs and tangents; no baked vertex lighting.

The moving-part slots remain separate: SolarArray_Wing faces +Y and tracks the sun; CommsArray_Dish faces +Z and scans; AtcTower_Radar spins around Y. Their model sockets are used by the renderer and build-menu previews. The solar gimbal pivot is at Y=0.98 and the full sweep remains inside its 2×2 footprint. BatteryBank_Charge and IceRefinery_Glow are native lit art in the existing live FX slots.

ModuleArtLighting binds each emissive material group to operating state, systems power and (when present) the existing FxLight source at its socket. This preserves sunlight dimming, flicker, beacons and running-state glow. The renderer still owns animations, charge levels, plume timing, refinery state, placement animation and simulation rules. All models keep the catalog footprints and foundation-level bases. Hangar retains an open central landing volume.

Rebuild: Waystation > Art > Rebuild Industrial Modules. Check: Waystation > Art > Check Industrial Modules. The check validates materials, winding, UVs, footprints, solar tracking clearance, moving-part loading, battery levels, refinery windows and disabled emissions. Actual Unity/URP renders and Validation.txt are saved under ArtPreviews/IndustrialModules/.
