# H01 Surface Habitat

First art pass for Regolith: stylized, colourful space hardware with a practical
pressure hull, insulated shell, landing feet, short airlock, photovoltaic wings,
roof thermal-control equipment, service tanks and a communications mast.

The ivory shell, teal systems, orange access and blue cells establish the starter
palette. The large shapes carry the design at the strategy camera's 45–70° pitch.
Exterior windows represent small protected viewports rather than a glass facade.
This exposed starter module assumes later shielding or regolith berm upgrades;
it does not depict a complete permanent radiation shelter.

## In Unity

`Resources/Modules/Habitat.prefab` and `HabCore.prefab` both use this starter model.
The existing ModuleRenderer picks them up automatically for completed modules.
Construction retains the existing placeholder. These are ordinary native Unity
assets, with no runtime model-building script or external package dependency.

- 1,426 triangles, 2,668 flat-shaded vertices, one mesh and one material.
- Ground-level pivot; front entrance points along -Z; Unity Y is up.
- Bounds: 1.566 × 0.8805 × 1.4145 tile units. Fits a radius-one pointy hex.
- At the source art scale, one Unity tile unit represents 10 metres.
- Uses the project's existing unlit vertex-colour shader and baked directional
  shading, so it remains visible without scene lights, like the terrain.
- No textures, UVs, collision mesh, interior or animation. Ground tile selection
  continues through the existing map collider.

## Source and review

`Habitat.obj` + `Habitat.mtl` are portable Y-up model sources in metres. Their
material palette is unbaked; the native Unity mesh has face lighting baked in.
The OBJ is an editing/export source; use the native prefabs in the game, since
the OBJ uses metre scale and Unity's default OBJ importer material settings.
`Habitat.geometry.json` preserves the authored polygon groups and palette.

Run `Tools/build_habitat.py` from the project root with Python 3 to regenerate
the mesh, material, prefabs and exports. Pillow and NumPy additionally produce
`ArtPreviews/Habitat.png`, a software-rendered sheet of the authored geometry.
Stable GUIDs preserve existing references when rebuilding. Rebuild after
changing the project's colour space so the vertex colours match the terrain.

Unity's `Regolith > Art > Check and Preview Habitat` menu checks both Resources
paths, the imported geometry and material, then creates an isolated Unity render
at `ArtPreviews/HabitatUnity.png` and writes `HabitatImportCheck.txt`. The check
also runs once after an editor reload when its report is missing or stale.
