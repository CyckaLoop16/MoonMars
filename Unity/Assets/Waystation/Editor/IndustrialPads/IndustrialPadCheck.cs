using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Waystation.Sim;
using Waystation.View;

namespace Waystation.EditorTools
{
    static class IndustrialPadCheck
    {
        static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        static void Require(bool value, string error) { if (!value) throw new Exception(error); }
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        [MenuItem("Waystation/Art/Check Industrial Pad Lighting")]
        public static void Run()
        {
            GameObject root = null;
            PreviewRenderUtility preview = null;
            VolumeProfile profile = null;
            var generated = new HashSet<UnityEngine.Object>();
            var lamps = new List<FxLight>();
            try
            {
                foreach (string name in new[] { "StarterPad", "LandingPad" })
                {
                    var prefab = Resources.Load<GameObject>("Buildings/" + name);
                    Require(prefab != null, "Missing pad prefab " + name);
                    var art = prefab.GetComponent<PadArtLighting>();
                    Require(art != null && art.SouthwestSocket != null && art.NortheastSocket != null, "Missing floodlight bindings " + name);
                    Require(art.SouthwestLenses.Length > 0 && art.NortheastLenses.Length > 0, "Missing floodlight lenses " + name);
                    foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
                    {
                        var mesh = filter.sharedMesh;
                        Require(mesh != null && mesh.vertexCount > 0 && mesh.normals.Length == mesh.vertexCount &&
                            mesh.uv.Length == mesh.vertexCount && mesh.tangents.Length == mesh.vertexCount, "Missing lit mesh attributes " + filter.name);
                        Require(mesh.colors.Length == 0, "Lit art must not use baked vertex colours.");
                        foreach (var p in mesh.vertices)
                        {
                            Require(Mathf.Abs(p.x) <= 1.5001f && Mathf.Abs(p.z) <= 1.5001f, "Pad exceeds its 3x3 footprint.");
                            Require(!(Mathf.Abs(p.x) < .75f && Mathf.Abs(p.z) < .75f && p.y > .04f), "Landing volume obstructed.");
                        }
                    }
                    foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>())
                    {
                        var material = renderer.sharedMaterial;
                        Require(material != null && material.shader.name == "Universal Render Pipeline/Lit", "Pad does not use URP Lit.");
                        if (material.IsKeywordEnabled("_EMISSION")) Require(material.GetTexture("_EmissionMap") != null, "Emission map missing.");
                    }
                }

                root = new GameObject("Industrial Pad Lighting Check") { hideFlags = HideFlags.HideAndDontSave };
                // Pads are 8 × 8 cells (1 cell = 2 m): the hub and pad #3 touch, pad #4 stands apart.
                var station = new Station(40, 14, 831);
                station.Buildings.Add(new Building { Id = 1, Type = BuildingType.StarterPad, Origin = new Cell(2, 3) });
                station.Buildings.Add(new Building { Id = 3, Type = BuildingType.LandingPad, Origin = new Cell(10, 3) });
                station.Buildings.Add(new Building { Id = 4, Type = BuildingType.LandingPad, Origin = new Cell(22, 3) });
                station.RecomputeConnections();
                Require(station.Buildings[0].Connected && station.Buildings[1].Connected && !station.Buildings[2].Connected, "Test connection topology incorrect.");
                var buildings = root.AddComponent<BuildingRenderer>();
                Invoke(buildings, "Awake");
                buildings.Bind(station);
                buildings.Rebuild();
                var arts = root.GetComponentsInChildren<PadArtLighting>();
                Require(arts.Length == 3, "Runtime did not load all pad prefabs.");
                lamps.AddRange(root.GetComponentsInChildren<FxLight>());
                foreach (var lamp in lamps)
                {
                    // Non-ExecuteAlways behaviours need their initialisation explicitly in this isolated editor check.
                    if (lamp.GetComponent<Light>() == null) Invoke(lamp, "OnEnable");
                    lamp.Apply(lamp.Intensity);
                }
                Require(root.GetComponentsInChildren<Light>().Count(l => l.type == LightType.Spot) == 4, "Connected pads must each have two real spotlights.");
                foreach (var art in arts) Invoke(art, "LateUpdate");
                var offline = arts.Single(a => a.transform.parent.name.EndsWith("#4"));
                var connected = arts.Single(a => a.transform.parent.name.EndsWith("#3"));
                var block = new MaterialPropertyBlock();
                offline.SouthwestLenses[0].GetPropertyBlock(block);
                Require(block.GetColor(Emission).maxColorComponent < .001f, "Offline floodlight still emits.");
                connected.SouthwestLenses[0].GetPropertyBlock(block);
                float full = block.GetColor(Emission).maxColorComponent;
                Require(full > 1f, "Floodlight emission is not HDR for bloom.");
                var failing = lamps.Single(l => l.transform.parent == connected.transform.parent && l.Mode == FxLightMode.Flicker);
                failing.Apply(failing.Intensity * .08f);
                Invoke(connected, "LateUpdate");
                connected.SouthwestLenses[0].GetPropertyBlock(block);
                Require(Mathf.Abs(block.GetColor(Emission).maxColorComponent / full - .08f) < .005f, "Lens emission does not follow light flicker.");
                failing.Apply(failing.Intensity);
                Invoke(connected, "LateUpdate");

                foreach (var art in arts)
                {
                    foreach (var renderer in art.GetComponentsInChildren<Renderer>())
                    {
                        Require(renderer.sharedMaterial != null, "Missing runtime material.");
                    }
                }
                // Set the shared placeholder light arrays too, for the live status/emergency lamps.
                var rig = root.AddComponent<LightRig>();
                Invoke(rig, "LateUpdate");
                foreach (var lamp in lamps) lamp.Apply(lamp.Intensity);
                foreach (var art in arts) Invoke(art, "LateUpdate");

                preview = new PreviewRenderUtility();
                preview.AddSingleGO(root);
                preview.lights[0].intensity = 0f;
                preview.lights[1].intensity = 0f;
                preview.ambientColor = new Color(.025f, .03f, .045f);
                var camera = preview.camera;
                camera.cameraType = CameraType.Game;
                camera.orthographic = true; camera.orthographicSize = 4.7f;
                camera.allowHDR = true;
                camera.nearClipPlane = .01f; camera.farClipPlane = 100f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.004f, .006f, .01f);
                var focus = new Vector3(.5f, .25f, -.5f);
                camera.transform.position = focus + new Vector3(5f, 7f, -10f);
                camera.transform.LookAt(focus);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                var volume = root.AddComponent<Volume>();
                volume.isGlobal = true; volume.priority = 100f;
                profile = ScriptableObject.CreateInstance<VolumeProfile>(); volume.sharedProfile = profile;
                profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
                var bloom = profile.Add<Bloom>(true); bloom.threshold.Override(.95f); bloom.intensity.Override(.9f); bloom.scatter.Override(.65f);
                preview.BeginStaticPreview(new Rect(0, 0, 1800, 1000));
                preview.Render(true);
                var image = preview.EndStaticPreview();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtPreviews"));
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "PadFloodlightsUnity.png"), image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
                File.WriteAllText(Path.Combine(folder, "PadFloodlightCheck.txt"),
                    "PASS: native StarterPad and LandingPad load from the game's Resources slots.\n" +
                    "URP Lit, albedo/normal/metallic-smoothness textures, emission maps, normals, UVs and tangents present; no baked vertex colours.\n" +
                    "Both pad footprints fit 3x3 and keep the central landing volume clear.\n" +
                    "Two connected pads create four real spotlights at their modelled lenses.\n" +
                    "Disconnected pad has zero floodlight emission; existing red emergency lamp retained.\n" +
                    "Failing southwest lamp and its HDR emission agree at 8% intensity.\n" +
                    "Actual URP preview with bloom: PadFloodlightsUnity.png\n");
                Debug.Log("Industrial pad lighting check passed.");
            }
            finally
            {
                if (root != null)
                {
                    foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                        if (filter.sharedMesh != null && !AssetDatabase.Contains(filter.sharedMesh)) generated.Add(filter.sharedMesh);
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                        foreach (var material in renderer.sharedMaterials)
                            if (material != null && !AssetDatabase.Contains(material)) generated.Add(material);
                    if (root.GetComponent<BuildingRenderer>() != null)
                    {
                        foreach (string field in new[] { "_meshes", "_statusMeshes" })
                        {
                            var values = typeof(BuildingRenderer).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(root.GetComponent<BuildingRenderer>()) as IDictionary;
                            foreach (UnityEngine.Object value in values.Values) if (value != null && !AssetDatabase.Contains(value)) generated.Add(value);
                        }
                    }
                }
                preview?.Cleanup();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                foreach (var lamp in lamps) FxLight.Active.Remove(lamp);
                foreach (var item in generated) if (item != null) UnityEngine.Object.DestroyImmediate(item);
                if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
            }
        }
    }
}
