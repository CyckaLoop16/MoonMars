using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Waystation.View;

namespace Waystation.EditorTools
{
    static class TileFixtureInstaller
    {
        const string Folder = "Assets/Waystation/Art/IndustrialPads";
        const string Prefabs = "Assets/Waystation/Resources/Buildings/";
        const string Stamp = Folder + "/TileLightRevision.txt";
        const string Revision = "1";
        static readonly string[] Names = { "Walkway_Lamp", "Walkway_InsetLight", "Foundation_EdgeLight", "Foundation_InsetLight" };

        [InitializeOnLoadMethod]
        static void OnLoad() => EditorApplication.delayCall += BuildIfNeeded;

        static void BuildIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/Pad_Steel.mat") == null) return;
            bool ready = File.Exists(Stamp) && File.ReadAllText(Stamp) == Revision;
            foreach (var name in Names) ready &= File.Exists(Prefabs + name + ".prefab");
            if (!ready) Build();
        }

        [MenuItem("Waystation/Art/Rebuild Walkway and Foundation Lights")]
        static void Build()
        {
            try
            {
                var metal = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/Pad_Steel.mat");
                var paint = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/Pad_Hull.mat");
                if (metal == null || paint == null) throw new Exception("Install the industrial pad materials first.");
                var warm = LensMaterial("Fixture_Sodium", BuildingRenderer.Sodium, false);
                var cold = LensMaterial("Fixture_Cold", MeshBuilder.Hex("#cfe0ff"), true);
                var metalCube = Cube("FixtureMetalCube", true);
                var plainCube = Cube("FixtureLensCube", false);
                var paintCube = Cube("FixturePaintCube", true, true);
                foreach (var name in Names)
                {
                    var root = new GameObject(name);
                    try
                    {
                        var fixture = root.AddComponent<TileLightFixture>();
                        var lenses = new List<Renderer>();
                        bool rail = name == "Walkway_Lamp";
                        bool edge = name == "Foundation_EdgeLight";
                        var lensMaterial = name.StartsWith("Foundation") ? cold : warm;
                        Action<string, Vector3, Vector3, bool, bool> box = (label, position, scale, lens, painted) =>
                        {
                            var go = new GameObject(label);
                            go.transform.SetParent(root.transform, false);
                            go.transform.localPosition = position; go.transform.localScale = scale;
                            go.AddComponent<MeshFilter>().sharedMesh = lens ? plainCube : painted ? paintCube : metalCube;
                            var renderer = go.AddComponent<MeshRenderer>();
                            renderer.sharedMaterial = lens ? lensMaterial : painted ? paint : metal;
                            if (lens) lenses.Add(renderer);
                        };
                        Vector3 socket;
                        if (rail)
                        {
                            box("Mounting shoe", new Vector3(.38f, .035f, .38f), new Vector3(.14f, .07f, .14f), false, false);
                            box("Rail lamp mast", new Vector3(.38f, .36f, .38f), new Vector3(.045f, .60f, .045f), false, false);
                            box("Weathered lamp head", new Vector3(.38f, .695f, .38f), new Vector3(.18f, .08f, .15f), false, true);
                            box("LED diffuser", new Vector3(.38f, .744f, .38f), new Vector3(.14f, .018f, .11f), true, false);
                            foreach (float x in new[] { .292f, .468f })
                                box("Lens guard", new Vector3(x, .735f, .38f), new Vector3(.014f, .06f, .15f), false, false);
                            socket = new Vector3(.38f, .79f, .38f);
                        }
                        else if (edge)
                        {
                            box("Edge light housing", new Vector3(0f, .052f, .45f), new Vector3(.70f, .075f, .09f), false, false);
                            box("Cold edge diffuser", new Vector3(0f, .096f, .45f), new Vector3(.60f, .015f, .044f), true, false);
                            foreach (float x in new[] { -.32f, .32f })
                                box("Protective end cap", new Vector3(x, .06f, .45f), new Vector3(.045f, .09f, .095f), false, true);
                            socket = new Vector3(0f, .20f, .36f);
                        }
                        else
                        {
                            foreach (float x in new[] { -.34f, .34f })
                            {
                                box("Recessed marker housing", new Vector3(x, .015f, 0f), new Vector3(.09f, .03f, .36f), false, false);
                                box("Floor guide diffuser", new Vector3(x, .035f, 0f), new Vector3(.045f, .01f, .28f), true, false);
                            }
                            socket = new Vector3(0f, .14f, 0f);
                        }
                        fixture.Lenses = lenses.ToArray();
                        var emitter = new GameObject("FX_Light").transform;
                        emitter.SetParent(root.transform, false); emitter.localPosition = socket;
                        fixture.Socket = emitter;
                        PrefabUtility.SaveAsPrefabAsset(root, Prefabs + name + ".prefab");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
                AssetDatabase.SaveAssets();
                TileLightingCheck.Run();
                File.WriteAllText(Stamp, Revision); AssetDatabase.ImportAsset(Stamp);
                Debug.Log("Waystation: walkway and foundation lights installed and checked.");
            }
            catch (Exception error) { Debug.LogError("Tile lighting installation: " + error); }
        }

        static Material LensMaterial(string name, Color color, bool solid)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            var source = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/Pad_FloodLamp.mat");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = material == null;
            if (create) material = new Material(source) { name = name };
            material.SetColor("_BaseColor", new Color(.02f, .025f, .03f));
            material.SetColor("_EmissionColor", color * (solid ? 2.1f : 2.6f));
            if (solid) material.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/Indicator_Emission.png"));
            material.EnableKeyword("_EMISSION");
            if (create) AssetDatabase.CreateAsset(material, path); else EditorUtility.SetDirty(material);
            return material;
        }

        static Mesh Cube(string name, bool atlas, bool paint = false)
        {
            var p = new[] { new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(.5f,.5f,-.5f), new Vector3(-.5f,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f), new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f) };
            var faces = new[] { new[] {0,3,2,1}, new[] {4,5,6,7}, new[] {0,4,7,3}, new[] {1,2,6,5}, new[] {3,7,6,2}, new[] {0,1,5,4} };
            var corners = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>();
            foreach (var face in faces)
            {
                int start = vertices.Count;
                var normal = Vector3.Cross(p[face[1]] - p[face[0]], p[face[2]] - p[face[0]]).normalized;
                for (int i = 0; i < 4; i++)
                {
                    vertices.Add(p[face[i]]); normals.Add(normal);
                    uv.Add(atlas ? new Vector2(.02f, paint ? .02f : .52f) + corners[i] * .45f : corners[i]);
                }
                indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            var fresh = new Mesh { name = name }; fresh.SetVertices(vertices); fresh.SetNormals(normals); fresh.SetUVs(0,uv); fresh.SetTriangles(indices,0);
            fresh.RecalculateTangents(); fresh.RecalculateBounds();
            string path = Folder + "/Meshes/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = fresh; AssetDatabase.CreateAsset(mesh,path); }
            else { EditorUtility.CopySerialized(fresh,mesh); UnityEngine.Object.DestroyImmediate(fresh); }
            return mesh;
        }
    }
}
