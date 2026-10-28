# L01 Lunar Surface Kit

Eight low-poly, flat-shaded models matching the H01 habitat's art language.
Muted warm highlands and cool basalt keep the colourful colony buildings readable.

| Prefab | Use |
| --- | --- |
| LunarHighlands | Bright anorthosite terrain with subdued relief and ejecta |
| LunarMare | Dark, relatively smooth basalt plain |
| SmallImpactCrater | Raised rim and shallow bowl |
| ShadowedCrater | Dark polar bowl with small muted ice patches |
| CraterRim | Asymmetric ridge segment and ejecta |
| LavaTubeEntrance | Stylized basalt skylight with a dark recessed throat |
| BoulderClusterA | Five darker basalt rocks |
| BoulderClusterB | Five paler highland rocks |

Native prefabs are under `Assets/Regolith/Resources/Terrain`. Each model uses one
mesh and the shared LunarPalette material, with the project's unlit vertex-colour
shader. Each tile has 458 triangles (464 for the ice-bearing crater); each rock
cluster has 190. The shader uses baked face lighting and needs no scene lights.

The six tile models share a radius-one pointy hex outline with boundary tops at
Y=0 and bottoms at Y=-0.18. They align with HexMapRenderer's grid and allow 60°
rotations. Interior relief can extend above and below zero. Rock cluster pivots
sit at ground level and need positioning onto the chosen terrain surface.

These are standalone art prefabs ready for placement or a terrain-rendering pass.
The current map renderer still builds its own terrain mesh. Replacing that mesh
requires matching collision, building ground height, resource indicators and
selection outlines to these surfaces. No simulation or terrain rules are changed.
The lava-tube model has a dark inset, without a traversable cave interior.

OBJ + MTL source exports use Unity Y-up and tile units, matching the native
prefabs (unlike H01's metre-scale OBJ). Multiply by ten for the habitat's source
metre scale. Geometry JSON preserves polygon groups and the base palette.

Regenerate with `Tools/build_lunar_surfaces.py` after `Tools/build_habitat.py`.
The lunar generator shares the habitat's serialization templates; stable GUIDs
preserve references. The build requires Python 3, Pillow and NumPy. Rebuild both
kits if the project's colour space changes.

`ArtPreviews/LunarSurface.png` is a software-rendered contact sheet of the source
geometry. `Regolith > Art > Check and Preview Lunar Surfaces` validates all eight
Unity imports, then saves actual Unity renders under `ArtPreviews/LunarUnity`
and an import report at `ArtPreviews/LunarImportCheck.txt`. This also runs after
an editor reload when the report is missing or stale.
