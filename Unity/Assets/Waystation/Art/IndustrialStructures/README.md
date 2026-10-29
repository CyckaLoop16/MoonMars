# Industrial walkway and foundation kit

Installed through the existing Resources/Buildings prefab slots. Uses the same URP Lit albedo, normal, metallic-smoothness maps and shared industrial materials as the pads. There are no baked vertex colours.

- Walkway_Deck: open metal grating, ivory edge armour, recessed cross-bracing, exposed utility pipes and small hazard patches. 628 triangles.
- Walkway_Railing: posts, kick plates, cross braces and a striped handrail along the +Z edge. 392 triangles.
- Foundation: layered armour, modular gunmetal plates, a flush olive inspection hatch, fasteners and an underside chassis. 688 triangles.
- Foundation_Edge: exposed-edge bumper, segmented ivory side plates, hazard trim and a recessed utility line along +Z. 232 triangles.

Each piece fits one 1×1 cell. The walkway and foundation deck level remains Y=0. Railings and foundation trim are placed only on open edges by the existing renderer. Joined sides stay open. Existing fixture slots, daylight/brownout handling, power controls, placement animations and module support rules require no runtime code changes.

Authoring: Assets/Waystation/Editor/IndustrialPads/IndustrialStructureInstaller.cs. Rebuild using Waystation > Art > Rebuild Industrial Walkways and Foundations. Validation: Waystation > Art > Check Industrial Structures.

Checks cover bounds, normals and winding, texture/material groups, actual grating openings, foundation detail heights, all 32 neighbour masks and the existing 64 fixture power/connection cases. Actual URP renders are saved as ArtPreviews/IndustrialStructuresDay.png and IndustrialStructuresNight.png. Validation details are in ArtPreviews/IndustrialStructuresCheck.txt.
