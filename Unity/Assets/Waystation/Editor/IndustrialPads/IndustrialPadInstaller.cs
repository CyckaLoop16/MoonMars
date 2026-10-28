using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Waystation.View;

namespace Waystation.EditorTools
{
    /// <summary>Converts authored pad data into native meshes, standard URP Lit materials and drop-in prefabs.</summary>
    static class IndustrialPadInstaller
    {
        const string Folder = "Assets/Waystation/Art/IndustrialPads";
        const string SourcePath = Folder + "/PadSource.json";
        const string StampPath = Folder + "/InstalledRevision.txt";
        const string PrefabFolder = "Assets/Waystation/Resources/Buildings";
        const string Revision = "5";

        [Serializable] public class Source { public int revision; public MaterialData[] materials; public ModelData[] models; }
        [Serializable] public class MaterialData { public string name; public float[] baseColor; public bool atlas; public float[] emission; public string emissionMap; }
        [Serializable] public class ModelData { public string name; public Part[] parts; public Anchor[] anchors; }
        [Serializable] public class Part { public string name; public string material; public string control; public float[] positions; public float[] normals; public float[] uv; }
        [Serializable] public class Anchor { public string name; public float[] position; public float[] forward; }

        [InitializeOnLoadMethod]
        static void OnLoad() => EditorApplication.delayCall += InstallIfChanged;

        static string Signature()
        {
            using (var hash = SHA256.Create())
                return Revision + ":" + BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(SourcePath)));
        }

        internal static void InstallIfChanged()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(SourcePath)) return;
            if (Shader.Find("Universal Render Pipeline/Lit") == null) return;
            string signature = Signature();
            if (File.Exists(StampPath) && File.ReadAllText(StampPath) == signature &&
                File.Exists(PrefabFolder + "/StarterPad.prefab") && File.Exists(PrefabFolder + "/LandingPad.prefab")) return;
            Install();
        }

        [MenuItem("Waystation/Art/Rebuild Industrial Pads")]
        static void Install()
        {
            try
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new Exception("URP Lit is unavailable. Run Waystation > Set Up URP first.");
                var source = JsonUtility.FromJson<Source>(File.ReadAllText(SourcePath));
                Directory.CreateDirectory(Folder + "/Materials");
                Directory.CreateDirectory(Folder + "/Meshes");
                Directory.CreateDirectory(PrefabFolder);
                AssetDatabase.Refresh();
                ConfigureTextures();
                var materials = new Dictionary<string, Material>();
                foreach (var data in source.materials) materials.Add(data.name, CreateMaterial(data, shader));
                foreach (var data in source.models) CreatePrefab(data, materials);
                AssetDatabase.SaveAssets();
                IndustrialPadCheck.Run();
                File.WriteAllText(StampPath, Signature());
                AssetDatabase.ImportAsset(StampPath);
                Debug.Log("Waystation: textured industrial pads and matching floodlight fixtures installed.");
            }
            catch (Exception exception) { Debug.LogError("Industrial pad installation: " + exception); }
        }

        static void ConfigureTextures()
        {
            foreach (var file in Directory.GetFiles(Folder + "/Textures", "*.png"))
            {
                string path = file.Replace('\\', '/');
                AssetDatabase.ImportAsset(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                bool normal = path.Contains("_Normal");
                bool data = normal || path.Contains("MetallicSmoothness");
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !data;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + name);
        static Color ColorOf(float[] values) => new Color(values[0], values[1], values[2], 1f);
        static Vector3 Vector(float[] values) => new Vector3(values[0], values[1], values[2]);

        static Material CreateMaterial(MaterialData data, Shader shader)
        {
            string path = Folder + "/Materials/Pad_" + data.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = material == null;
            if (create) material = new Material(shader) { name = "Pad_" + data.name };
            material.shader = shader;
            material.SetColor("_BaseColor", ColorOf(data.baseColor));
            material.SetFloat("_Smoothness", .3f);
            material.SetFloat("_Metallic", .05f);
            if (data.atlas)
            {
                material.SetTexture("_BaseMap", Texture("IndustrialAtlas_BaseColor.png"));
                material.SetTexture("_BumpMap", Texture("IndustrialAtlas_Normal.png"));
                material.SetFloat("_BumpScale", .35f);
                material.SetTexture("_MetallicGlossMap", Texture("IndustrialAtlas_MetallicSmoothness.png"));
                material.SetFloat("_Smoothness", 1f);
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_SmoothnessTextureChannel", 0f);
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (!string.IsNullOrEmpty(data.emissionMap))
            {
                material.SetTexture("_EmissionMap", Texture(data.emissionMap));
                material.SetColor("_EmissionColor", ColorOf(data.emission));
                material.EnableKeyword("_EMISSION");
            }
            else
            {
                material.SetColor("_EmissionColor", Color.black);
                material.DisableKeyword("_EMISSION");
            }
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.enableInstancing = true;
            if (create) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
        }

        static void CreatePrefab(ModelData data, Dictionary<string, Material> materials)
        {
            var root = new GameObject(data.name);
            try
            {
                var bindings = root.AddComponent<PadArtLighting>();
                var southwest = new List<Renderer>(); var northeast = new List<Renderer>();
                var beacon = new List<Renderer>(); var powered = new List<Renderer>();
                foreach (var part in data.parts)
                {
                    int count = part.positions.Length / 3;
                    var vertices = new Vector3[count]; var normals = new Vector3[count]; var uv = new Vector2[count]; var indices = new int[count];
                    for (int i = 0; i < count; i++)
                    {
                        vertices[i] = new Vector3(part.positions[i * 3], part.positions[i * 3 + 1], part.positions[i * 3 + 2]);
                        normals[i] = new Vector3(part.normals[i * 3], part.normals[i * 3 + 1], part.normals[i * 3 + 2]);
                        // Authored atlas UVs use glTF's top-left origin; native Unity UVs use bottom-left.
                        uv[i] = new Vector2(part.uv[i * 2], 1f - part.uv[i * 2 + 1]);
                        indices[i] = i;
                    }
                    string name = data.name + "_" + part.name.Replace(' ', '_');
                    var fresh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                    fresh.vertices = vertices; fresh.normals = normals; fresh.uv = uv; fresh.triangles = indices;
                    fresh.RecalculateTangents(); fresh.RecalculateBounds();
                    string path = Folder + "/Meshes/" + name + ".asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (mesh == null) { mesh = fresh; AssetDatabase.CreateAsset(mesh, path); }
                    else { EditorUtility.CopySerialized(fresh, mesh); UnityEngine.Object.DestroyImmediate(fresh); }
                    var child = new GameObject(part.name);
                    child.transform.SetParent(root.transform, false);
                    child.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = child.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = materials[part.material];
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    if (part.control == "Flood_SW") southwest.Add(renderer);
                    else if (part.control == "Flood_NE") northeast.Add(renderer);
                    else if (part.control == "Beacon lens") beacon.Add(renderer);
                    else if (part.control == "Powered glow") powered.Add(renderer);
                }
                foreach (var anchor in data.anchors)
                {
                    var child = new GameObject(anchor.name).transform;
                    child.SetParent(root.transform, false);
                    child.localPosition = Vector(anchor.position);
                    child.localRotation = Quaternion.LookRotation(Vector(anchor.forward),
                        Mathf.Abs(Vector(anchor.forward).y) > .99f ? Vector3.forward : Vector3.up);
                    if (anchor.name == "FX_Flood_SW") bindings.SouthwestSocket = child;
                    if (anchor.name == "FX_Flood_NE") bindings.NortheastSocket = child;
                    if (anchor.name == "FX_Beacon") bindings.BeaconSocket = child;
                }
                bindings.SouthwestLenses = southwest.ToArray(); bindings.NortheastLenses = northeast.ToArray();
                bindings.BeaconLenses = beacon.ToArray(); bindings.PoweredIndicators = powered.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + data.name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
    sealed class IndustrialPadSourceWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (Array.IndexOf(imported, "Assets/Waystation/Art/IndustrialPads/PadSource.json") >= 0)
                EditorApplication.delayCall += IndustrialPadInstaller.InstallIfChanged;
        }
    }
}
