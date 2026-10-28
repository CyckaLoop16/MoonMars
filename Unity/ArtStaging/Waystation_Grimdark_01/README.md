# Waystation — Grimdark art study 01

Original review exports remain staged outside Unity’s Assets folder. A later, approved pad revision with modelled floodlights is now installed under Assets/Waystation/Art/IndustrialPads and Resources/Buildings. The background exports remain staged.

## Models

- LandingPad_Industrial: 3 × 3 footprint, centred pivot, deck at Y=0.
- StarterPad_Industrial: the same platform with a fuel reservoir and service console.
- RingedGasGiant_Industrial: smooth 3D sphere, UV-mapped cloud bands and separate annular ring geometry.
- DistantMoon_Industrial: a rocky moon.
- Asteroid_Basalt_Industrial, Asteroid_Warm_Industrial and Asteroid_Icy_Industrial: three fractured silhouettes.

The pad has segmented heat-resistant plating, inset guidance rings, safety markings, cardinal corridor aprons, layered armour, an exposed support truss, radiator fins and underside attitude thrusters. Its central landing volume remains clear.

## Files

Models/ contains GLB and OBJ + MTL exports. Each GLB embeds its images, UVs, normals, material groups and PBR material assignments; it can be opened independently. OBJ + MTL uses adjacent Textures/ files. OBJ support for normal/roughness/metallic extensions varies by viewer, so GLB is the preferred review format. The ring material is double-sided.

Textures/ contains:

- IndustrialAtlas_BaseColor.png — the unmodified 1254 × 1254 generated colour atlas: charcoal metal, olive, dirty ivory and oxide-red paint.
- IndustrialAtlas_Normal.png — 2048 × 2048 tangent-space microdetail normal map, OpenGL/+Y convention.
- IndustrialAtlas_ORM.png — 2048 × 2048 packed R=1, G=roughness, B=metallic. AO is neutral.
- IndustrialAtlas_Roughness.png and IndustrialAtlas_Metallic.png — separate grayscale data maps.
- GasGiant_BaseColor.png — 4096 × 2048 equirectangular cloud map.
- Rock_BaseColor.png, Rock_Normal.png and Rock_ORM.png — separate 2048 × 2048 rock maps.

Technical maps are procedurally authored surface data. The metal normal adds micro-pitting and scratches; it does not claim to reconstruct every chip in the generated colour image. The cloud and mineral maps are procedurally authored. The artistic metal colour atlas was created with the built-in image-generation tool; its prompt is recorded in TexturePrompt.md.

Previews/ contains offline orthographic review sheets of the actual authored geometry with its UV-mapped colour textures. These are material studies, not screenshots of the game. The preview renderer uses mesh normals and simplified lighting; the embedded normal and metallic/roughness maps are for a PBR renderer. The atmospheric limb in the planet preview is a presentation effect and is not an extra atmosphere mesh or shader.

## Installed pad revision

The approved pad revision uses native Unity meshes and standard URP Lit materials with actual emission maps. Two angled floodlights occupy the SW/NE corners at one unit high and share Claude’s FxLight/LightRig power and flicker controls. The starter reservoir, south pump, status mount and communications beacon match the runtime FX locations. See Assets/Waystation/Art/IndustrialPads/README.md for the active integration.

Source/prepare_pad_install.py produces UnityPadRevision/PadSource.json and texture inputs. The native Unity installer creates the prefab and material assets. The original GLB/OBJ exports in Models/ remain the earlier comparison studies. Background installation is still pending a separate request.

Source/build_textured_models.py and Source/mesh_authoring.py preserve the original authoring pipeline. Rebuild with Python 3, NumPy and Pillow. Keep the generated colour atlas in Textures/. The brighter alternative is retained separately in ../Waystation_Art_01.
