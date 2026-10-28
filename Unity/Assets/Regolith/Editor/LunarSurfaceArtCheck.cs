using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Regolith.Editor
{
    static class LunarSurfaceArtCheck
    {
        static readonly string[] Names = { "LunarHighlands", "LunarMare", "SmallImpactCrater",
            "ShadowedCrater", "CraterRim", "LavaTubeEntrance", "BoulderClusterA", "BoulderClusterB" };

        [InitializeOnLoadMethod]
        static void OnLoad() => EditorApplication.delayCall += CheckOnce;

        static void CheckOnce()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtPreviews"));
            string source = Path.Combine(Application.dataPath, "Regolith/Art/LunarSurface/LunarSurface.stats.json");
            string report = Path.Combine(folder, "LunarImportCheck.txt");
            if (!File.Exists(source)) return;
            if (File.Exists(report) && File.GetLastWriteTimeUtc(report) >= File.GetLastWriteTimeUtc(source)) return;
            CheckAndPreview();
        }

        [MenuItem("Regolith/Art/Check and Preview Lunar Surfaces")]
        static void CheckAndPreview()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtPreviews/LunarUnity"));
            Directory.CreateDirectory(folder);
            var report = new StringBuilder("PASS: Unity " + Application.unityVersion + "\n");
            try
            {
                foreach (string name in Names)
                {
                    var prefab = Resources.Load<GameObject>("Terrain/" + name);
                    if (prefab == null) throw new Exception("Missing terrain prefab: " + name);
                    var mesh = prefab.GetComponent<MeshFilter>().sharedMesh;
                    var material = prefab.GetComponent<MeshRenderer>().sharedMaterial;
                    if (mesh == null || mesh.vertexCount == 0 || mesh.colors32.Length != mesh.vertexCount || mesh.normals.Length != mesh.vertexCount)
                        throw new Exception("Missing mesh data: " + name);
                    if (material == null || material.shader == null || !material.shader.isSupported)
                        throw new Exception("Unsupported material: " + name);
                    var vertices = mesh.vertices;
                    var triangles = mesh.triangles;
                    foreach (var p in vertices)
                        if (Mathf.Abs(p.x) > .8661f || Mathf.Abs(p.z) + Mathf.Abs(p.x) / Mathf.Sqrt(3f) > 1.001f)
                            throw new Exception("Model exceeds hex boundary: " + name);
                    for (int i = 0; i < triangles.Length; i += 3)
                        if (Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]],
                            vertices[triangles[i + 2]] - vertices[triangles[i]]).sqrMagnitude < 1e-16f)
                            throw new Exception("Degenerate triangle: " + name);

                    var preview = new PreviewRenderUtility();
                    try
                    {
                        preview.AddSingleGO(UnityEngine.Object.Instantiate(prefab));
                        var camera = preview.camera;
                        camera.orthographic = true;
                        camera.orthographicSize = name.StartsWith("Boulder") ? .55f : 1.12f;
                        camera.nearClipPlane = .01f;
                        camera.farClipPlane = 20f;
                        camera.clearFlags = CameraClearFlags.SolidColor;
                        camera.backgroundColor = new Color(.075f, .118f, .169f);
                        camera.transform.position = new Vector3(1.8f, 2.8f, -3.3f);
                        camera.transform.LookAt(new Vector3(0f, .02f, 0f));
                        preview.BeginStaticPreview(new Rect(0, 0, 640, 640));
                        preview.Render();
                        var image = preview.EndStaticPreview();
                        File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(image);
                    }
                    finally { preview.Cleanup(); }
                    report.AppendLine(name + ": " + (triangles.Length / 3) + " triangles; geometry, material, bounds and render OK.");
                }
                File.WriteAllText(Path.Combine(folder, "../LunarImportCheck.txt"), report.ToString());
                Debug.Log("Lunar surface art check passed for all eight models.");
            }
            catch (Exception exception) { Debug.LogError("Lunar surface art check: " + exception); }
        }
    }
}
