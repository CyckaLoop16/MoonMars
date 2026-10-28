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
    static class TileLightingCheck
    {
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        static int OpenSides(int mask) { int count = 0; for (int i = 0; i < 4; i++) if ((mask & (1 << i)) == 0) count++; return count; }

        [MenuItem("Waystation/Art/Check Walkway and Foundation Lights")]
        public static void Run()
        {
            var root = new GameObject("Tile Lighting Check") { hideFlags = HideFlags.HideAndDontSave };
            PreviewRenderUtility preview = null;
            VolumeProfile profile = null;
            var temporary = new HashSet<UnityEngine.Object>();
            try
            {
                var renderer = root.AddComponent<BuildingRenderer>(); Invoke(renderer, "Awake");
                // Every connection mask, both power states: joined sides stay open; junctions use floor guides.
                foreach (bool foundation in new[] { false, true })
                for (int mask = 0; mask < 16; mask++)
                foreach (bool powered in new[] { false, true })
                {
                    var tile = new GameObject("Mask check");
                    try
                    {
                        var building = new Building { Id = 9, Type = foundation ? BuildingType.Foundation : BuildingType.Walkway,
                            Origin = new Cell(4,4), Connected = powered };
                        var method = typeof(BuildingRenderer).GetMethod(foundation ? "AddFoundationLights" : "AddWalkwayLamp", BindingFlags.Instance | BindingFlags.NonPublic);
                        method.Invoke(renderer, new object[] { tile, building, mask, powered });
                        var fixtures = tile.GetComponentsInChildren<TileLightFixture>();
                        Require(fixtures.Length == (foundation && mask != 15 ? OpenSides(mask) : 1), "Incorrect fixture count for mask " + mask);
                        Require(tile.GetComponentsInChildren<FxLight>().Length <= (powered ? 1 : 0), "Too many area lights on a tile.");
                        if (foundation && mask != 15)
                            foreach (var fixture in fixtures)
                            {
                                var direction = fixture.transform.forward;
                                int side = Mathf.Abs(direction.x) > .5f ? direction.x > 0f ? 1 : 3 : direction.z > 0f ? 0 : 2;
                                Require((mask & (1 << side)) == 0, "Light trim crosses a joined edge.");
                            }
                        foreach (var fixture in fixtures)
                        {
                            Require(fixture.Socket != null && fixture.Lenses.Length > 0, "Fixture is missing its lens or light socket.");
                            foreach (var filter in fixture.GetComponentsInChildren<MeshFilter>())
                                foreach (var vertex in filter.sharedMesh.vertices)
                                {
                                    var p = tile.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                                    Require(Mathf.Abs(p.x) <= .501f && Mathf.Abs(p.z) <= .501f, "Fixture exceeds tile boundary.");
                                    Require(!(Mathf.Abs(p.x) < .25f && Mathf.Abs(p.z) < .25f && p.y > .05f), "Lamp blocks the central path.");
                                }
                            if (!powered)
                            {
                                Invoke(fixture, "LateUpdate");
                                var block = new MaterialPropertyBlock();
                                fixture.Lenses[0].GetPropertyBlock(block);
                                Require(block.GetColor(Emission).maxColorComponent < .001f, "Disconnected fixture still emits.");
                            }
                        }
                    }
                    finally
                    {
                        foreach (var lamp in tile.GetComponentsInChildren<FxLight>()) FxLight.Active.Remove(lamp);
                        UnityEngine.Object.DestroyImmediate(tile);
                    }
                }

                var station = new Station(16,14,912);
                station.Buildings.Add(new Building { Id = 1, Type = BuildingType.StarterPad, Origin = new Cell(1,6) });
                int id = 2;
                for (int x = 4; x <= 9; x++) station.Buildings.Add(new Building { Id = id++, Type = BuildingType.Walkway, Origin = new Cell(x,7) });
                foreach (int y in new[] {6,8}) station.Buildings.Add(new Building { Id = id++, Type = BuildingType.Walkway, Origin = new Cell(7,y) });
                for (int x = 10; x <= 12; x++) for (int y = 6; y <= 8; y++)
                    station.Buildings.Add(new Building { Id = id++, Type = BuildingType.Foundation, Origin = new Cell(x,y) });
                station.Buildings.Add(new Building { Id = id++, Type = BuildingType.Walkway, Origin = new Cell(9,1) });
                station.Buildings.Add(new Building { Id = id++, Type = BuildingType.Foundation, Origin = new Cell(13,1) });
                station.RecomputeConnections(); renderer.Bind(station); renderer.Rebuild();
                var lamps = root.GetComponentsInChildren<FxLight>();
                foreach (var lamp in lamps)
                {
                    if (lamp.GetComponent<Light>() == null) Invoke(lamp,"OnEnable");
                    lamp.Apply(lamp.Intensity);
                }
                foreach (var fixture in root.GetComponentsInChildren<TileLightFixture>()) Invoke(fixture,"LateUpdate");
                foreach (var pad in root.GetComponentsInChildren<PadArtLighting>()) Invoke(pad,"LateUpdate");
                foreach (var building in station.Buildings.Where(b => b.Type == BuildingType.Walkway || b.Type == BuildingType.Foundation))
                {
                    var tile = root.transform.Find(station.Label(building) + " #" + building.Id);
                    Require(tile != null, "Missing runtime tile.");
                    Require(tile.GetComponentsInChildren<TileLightFixture>().Length > 0, "Tile has no installed light fixtures.");
                    if (building.Connected)
                    {
                        var block = new MaterialPropertyBlock();
                        tile.GetComponentInChildren<TileLightFixture>().Lenses[0].GetPropertyBlock(block);
                        Require(block.GetColor(Emission).maxColorComponent > 1f, "Powered fixture does not emit HDR light.");
                    }
                }
                var rig = root.AddComponent<LightRig>(); Invoke(rig,"LateUpdate");
                foreach (var lamp in lamps) lamp.Apply(lamp.Intensity);
                foreach (var fixture in root.GetComponentsInChildren<TileLightFixture>()) Invoke(fixture,"LateUpdate");
                foreach (var pad in root.GetComponentsInChildren<PadArtLighting>()) Invoke(pad,"LateUpdate");

                preview = new PreviewRenderUtility(); preview.AddSingleGO(root);
                preview.lights[0].intensity = preview.lights[1].intensity = 0f;
                preview.ambientColor = new Color(.025f,.03f,.045f);
                var camera = preview.camera;
                camera.cameraType = CameraType.Game; camera.orthographic = true; camera.orthographicSize = 5.8f;
                camera.allowHDR = true; camera.nearClipPlane = .01f; camera.farClipPlane = 100f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.004f,.006f,.01f);
                var focus = new Vector3(-.5f,.1f,-1.2f);
                camera.transform.position = focus + new Vector3(8f,12f,-14f); camera.transform.LookAt(focus);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                var volume = root.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 100f;
                profile = ScriptableObject.CreateInstance<VolumeProfile>(); volume.sharedProfile = profile;
                profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
                var bloom = profile.Add<Bloom>(true); bloom.threshold.Override(.95f); bloom.intensity.Override(.9f); bloom.scatter.Override(.65f);
                preview.BeginStaticPreview(new Rect(0,0,1800,1100)); preview.Render(true);
                var image = preview.EndStaticPreview();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath,"../ArtPreviews")); Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder,"WalkwayFoundationLightsUnity.png"),image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
                File.WriteAllText(Path.Combine(folder,"TileLightingCheck.txt"),
                    "PASS: all 16 connection masks for walkways and foundations, powered and unpowered (64 cases).\n" +
                    "Joined foundation edges have no lamp trim; all fixtures fit one tile and leave the central route clear.\n" +
                    "At most one area light per tile; disconnected fixtures emit zero light.\n" +
                    "Runtime station loads installed fixtures; connected lenses emit HDR light.\n" +
                    "URP + bloom preview: WalkwayFoundationLightsUnity.png\n");
                Debug.Log("Walkway and foundation lighting checks passed.");
            }
            finally
            {
                if (root != null)
                {
                    foreach (var lamp in root.GetComponentsInChildren<FxLight>()) FxLight.Active.Remove(lamp);
                    foreach (var filter in root.GetComponentsInChildren<MeshFilter>()) if (filter.sharedMesh != null && !AssetDatabase.Contains(filter.sharedMesh)) temporary.Add(filter.sharedMesh);
                    foreach (var r in root.GetComponentsInChildren<Renderer>()) foreach (var m in r.sharedMaterials) if (m != null && !AssetDatabase.Contains(m)) temporary.Add(m);
                    var renderer = root.GetComponent<BuildingRenderer>();
                    foreach (string name in new[] { "_meshes", "_statusMeshes" })
                    {
                        var dictionary = typeof(BuildingRenderer).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(renderer) as IDictionary;
                        foreach (UnityEngine.Object value in dictionary.Values) if (value != null && !AssetDatabase.Contains(value)) temporary.Add(value);
                    }
                }
                preview?.Cleanup(); if (root != null) UnityEngine.Object.DestroyImmediate(root);
                foreach (var item in temporary) if (item != null) UnityEngine.Object.DestroyImmediate(item);
                if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
            }
        }
    }
}
