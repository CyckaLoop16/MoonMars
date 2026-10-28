using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Regolith.Editor
{
    /// <summary>Checks the imported art and renders it without changing the open scene.</summary>
    static class HabitatArtCheck
    {
        const string Art = "Assets/Regolith/Art/Habitat/";

        [InitializeOnLoadMethod]
        static void OnLoad()
        {
            EditorApplication.delayCall += CheckOnce;
        }

        static void CheckOnce()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string report = Path.Combine(Application.dataPath, "../ArtPreviews/HabitatImportCheck.txt");
            string source = Path.Combine(Application.dataPath, "Regolith/Art/Habitat/HabitatMesh.asset");
            if (!File.Exists(source)) return;
            if (File.Exists(report) && File.GetLastWriteTimeUtc(report) >= File.GetLastWriteTimeUtc(source)) return;
            CheckAndPreview();
        }

        [MenuItem("Regolith/Art/Check and Preview Habitat")]
        static void CheckAndPreview()
        {
            PreviewRenderUtility preview = null;
            Texture2D image = null;
            try
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "HabitatMesh.asset");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Regolith/Resources/Modules/Habitat.prefab");
                if (mesh == null || prefab == null) throw new Exception("Habitat mesh or prefab did not import.");
                if (mesh.vertexCount != 2668 || mesh.triangles.Length != 4278)
                    throw new Exception("Imported geometry does not match the authored mesh.");
                if (mesh.colors32.Length != mesh.vertexCount || mesh.normals.Length != mesh.vertexCount)
                    throw new Exception("Vertex colours or normals are missing.");
                foreach (string name in new[] { "Habitat", "HabCore" })
                {
                    var asset = Resources.Load<GameObject>("Modules/" + name);
                    if (asset == null || asset.GetComponent<MeshFilter>().sharedMesh != mesh)
                        throw new Exception(name + " does not resolve through the game's Resources path.");
                    var material = asset.GetComponent<MeshRenderer>().sharedMaterial;
                    if (material == null || material.shader == null || !material.shader.isSupported)
                        throw new Exception(name + " material is unavailable or unsupported.");
                }

                preview = new PreviewRenderUtility();
                var instance = UnityEngine.Object.Instantiate(prefab);
                preview.AddSingleGO(instance);
                var camera = preview.camera;
                camera.orthographic = true;
                camera.orthographicSize = .94f;
                camera.nearClipPlane = .01f;
                camera.farClipPlane = 20f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.075f, .118f, .169f);
                camera.transform.position = new Vector3(2.0f, 2.0f, -3.2f);
                camera.transform.LookAt(new Vector3(0f, .36f, -.06f));
                preview.BeginStaticPreview(new Rect(0, 0, 1200, 1200));
                preview.Render();
                image = preview.EndStaticPreview();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtPreviews"));
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "HabitatUnity.png"), image.EncodeToPNG());
                File.WriteAllText(Path.Combine(folder, "HabitatImportCheck.txt"),
                    "PASS: Unity " + Application.unityVersion + "\n" +
                    "Both Resources prefabs resolve; mesh, normals, colours and shader validated.\n" +
                    "2668 vertices / 1426 triangles / one material.\n" +
                    "Rendered HabitatUnity.png in an isolated editor preview scene.\n");
                Debug.Log("Habitat art check passed. See ArtPreviews/HabitatUnity.png.");
            }
            catch (Exception exception)
            {
                Debug.LogError("Habitat art check: " + exception);
            }
            finally
            {
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                preview?.Cleanup();
            }
        }
    }
}
