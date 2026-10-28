# Industrial pads with floodlights

Native StarterPad and LandingPad prefabs are built into the existing Resources/Buildings slots. These use standard Universal Render Pipeline/Lit materials, actual albedo, normal, metallic-smoothness and emission maps, with no baked vertex lighting.

Two modelled floodlight heads sit at local (-1.2, 1.0, -1.2) and (1.2, 1.0, 1.2), aimed inward at the deck. BuildingRenderer uses its existing FxLight and LightRig drivers for both real Unity spotlights and emissive lens brightness. Southwest flicker follows the existing building-ID rule. An unconnected pad has no active floodlights or powered emission; the existing emergency beacon and pad status logic remain responsible for their own display.

The starter tank is in the northwest corner. The south-edge pump matches the existing refuelling-vapour location. The starter communications beacon matches its existing light at (1.05, 1.75, -1.05). FX_Flood_SW, FX_Flood_NE, FX_Status, FX_Fuel and FX_Beacon (starter only) record the attachment locations. Lens emission is mask-driven, with dark non-emissive housings.

PadSource.json contains authored geometry and UVs. IndustrialPadInstaller rebuilds native mesh/material/prefab assets when this source revision changes. Manual rebuild: Waystation > Art > Rebuild Industrial Pads. Validation: Waystation > Art > Check Industrial Pad Lighting. The validation creates an isolated URP preview, checks Resources loading, standard lit material maps, mesh attributes, footprint and landing clearance, real spotlights, offline emission and matched flicker. Results are written outside Assets under ArtPreviews/.

The source generator is ArtStaging/Waystation_Grimdark_01/Source/prepare_pad_install.py. Technical map conversion packs metallic into R and smoothness into A for URP. Source maps remain in the staging package. Background models are still staged separately.

## Walkway and foundation lights

Walkway_Lamp is a compact sodium rail lamp; each exposed walkway tile gets one. Fully enclosed junctions use Walkway_InsetLight floor guides, keeping the centre clear. Foundation_EdgeLight appears only on exposed edges, with Foundation_InsetLight marking enclosed floor tiles. Foundations have at most one area light each; enclosed foundation area lights alternate by cell parity. All new fixtures use URP Lit materials and emission maps, share existing FxLight/LightRig control, and switch their emission off when disconnected. Existing red emergency lights remain active under the original rules.

Rebuild: Waystation > Art > Rebuild Walkway and Foundation Lights. Check: Waystation > Art > Check Walkway and Foundation Lights. The check covers all 16 connection masks for both tile types and power states, boundary/path clearance, a connected station and offline tiles. It writes ArtPreviews/TileLightingCheck.txt and WalkwayFoundationLightsUnity.png.
